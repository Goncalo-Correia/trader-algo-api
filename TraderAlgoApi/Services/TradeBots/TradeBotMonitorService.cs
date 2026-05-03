using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.TradeEvents;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.MarketData;
using TraderAlgoApi.Services.TradeEvents;
using TraderAlgoApi.Services.Trades;

namespace TraderAlgoApi.Services.TradeBots;

public sealed class TradeBotMonitorService(
    ClosedCandleFeed closedCandleFeed,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ITradeEventPublisher tradeEventPublisher,
    ILogger<TradeBotMonitorService> logger) : BackgroundService
{
    private readonly Channel<ClosedCandleEvent> _channel =
        Channel.CreateBounded<ClosedCandleEvent>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        closedCandleFeed.CandleClosed += OnCandleClosed;

        try
        {
            await foreach (var candle in _channel.Reader.ReadAllAsync(stoppingToken))
                await EvaluateAsync(candle, stoppingToken);
        }
        finally
        {
            closedCandleFeed.CandleClosed -= OnCandleClosed;
            _channel.Writer.TryComplete();
        }
    }

    private void OnCandleClosed(ClosedCandleEvent candle) =>
        _channel.Writer.TryWrite(candle);

    private async Task EvaluateAsync(ClosedCandleEvent candle, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var signalService = scope.ServiceProvider.GetRequiredService<ITradeBotSignalService>();
            var tradeService = scope.ServiceProvider.GetRequiredService<ITradeService>();

            var tradeBots = await dbContext.TradeBots
                .Include(b => b.TradingAccount)
                .Include(b => b.Symbol)
                .Include(b => b.Interval)
                .Where(b => b.IsEnabled &&
                            b.TradingAccount.IsActive &&
                            b.Symbol.Code == candle.Symbol &&
                            b.Interval.Code == candle.Interval)
                .ToListAsync(cancellationToken);

            foreach (var tradeBot in tradeBots)
                await EvaluateTradeBotAsync(dbContext, tradeBot, signalService, tradeService, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Error evaluating tradebots for closed candle {Symbol}/{Interval}",
                candle.Symbol,
                candle.Interval);
        }
    }

    private async Task EvaluateTradeBotAsync(
        ApplicationDbContext dbContext,
        Models.TradeBot tradeBot,
        ITradeBotSignalService signalService,
        ITradeService tradeService,
        CancellationToken cancellationToken)
    {
        var signal = await signalService.EvaluateAsync(tradeBot, cancellationToken);
        if (signal.Signal == TradeBotSignal.None)
        {
            logger.LogDebug(
                "Tradebot {TradeBotId} ignored candle: {Reason}",
                tradeBot.Id,
                signal.Reason);
            return;
        }

        tradeBot.LastSignalAt = timeProvider.GetUtcNow();

        var side = signal.Signal == TradeBotSignal.EnterLong
            ? TradeSide.Buy
            : TradeSide.Sell;

        var openTrade = await dbContext.Trades
            .Where(t => t.BacktestId == null &&
                        t.TradingAccountId == tradeBot.TradingAccountId &&
                        (t.StatusId == (int)TradeStatus.Active || t.StatusId == (int)TradeStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (openTrade is not null)
        {
            if (openTrade.StatusId == (int)TradeStatus.Active &&
                openTrade.SideId != (int)side)
            {
                await tradeService.CloseAsync(openTrade.Id, TradeCloseReason.BotSignal, cancellationToken);
                logger.LogInformation(
                    "Tradebot {TradeBotId} closed trade {TradeId} for account {AccountId} on opposite signal",
                    tradeBot.Id,
                    openTrade.Id,
                    tradeBot.TradingAccountId);
                return;
            }

            tradeEventPublisher.Publish(new TradeEventDto(
                Type: "SignalIgnored",
                TradingAccountId: tradeBot.TradingAccountId,
                TradeId: openTrade.Id,
                SymbolCode: tradeBot.Symbol.Code,
                Message: "A pending or same-direction active trade already exists.",
                CreatedAt: timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                Trade: null));

            return;
        }

        try
        {
            await tradeService.CreateAsync(
                new CreateTradeRequestDto(
                    SymbolCode: tradeBot.Symbol.Code,
                    IntervalCode: tradeBot.Interval.Code,
                    Side: side,
                    OrderType: TradeOrderType.Market,
                    Quantity: tradeBot.Quantity,
                    LimitPrice: null,
                    StopLoss: tradeBot.StopLoss,
                    TakeProfit: tradeBot.TakeProfit,
                    TradingAccountId: tradeBot.TradingAccountId),
                cancellationToken);

            logger.LogInformation(
                "Tradebot {TradeBotId} entered {Side} trade for account {AccountId}",
                tradeBot.Id,
                side,
                tradeBot.TradingAccountId);
        }
        catch (InvalidOperationException ex)
        {
            tradeEventPublisher.Publish(new TradeEventDto(
                Type: "SignalIgnored",
                TradingAccountId: tradeBot.TradingAccountId,
                TradeId: null,
                SymbolCode: tradeBot.Symbol.Code,
                Message: ex.Message,
                CreatedAt: timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                Trade: null));

            logger.LogInformation(
                ex,
                "Tradebot {TradeBotId} signal ignored for account {AccountId}",
                tradeBot.Id,
                tradeBot.TradingAccountId);
        }
    }
}
