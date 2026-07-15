using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.TradeEvents;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Backtests;
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
                .Include(b => b.TradingStrategy)
                .Include(b => b.MlPolicy).ThenInclude(p => p!.Symbol)
                .Include(b => b.MlPolicy).ThenInclude(p => p!.Interval)
                .Include(b => b.Symbol)
                .Include(b => b.Interval)
                .Where(b => b.IsEnabled &&
                            b.TradingAccountId != null &&
                            b.BacktestId == null &&
                            b.TradingAccount != null &&
                            b.TradingAccount.IsActive &&
                            b.Symbol.Code == candle.Symbol &&
                            b.Interval.Code == candle.Interval)
                .ToListAsync(cancellationToken);

            foreach (var tradeBot in tradeBots)
                await EvaluateTradeBotAsync(dbContext, tradeBot, candle, signalService, tradeService, cancellationToken);
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
        ClosedCandleEvent candle,
        ITradeBotSignalService signalService,
        ITradeService tradeService,
        CancellationToken cancellationToken)
    {
        var candleOpenTime = candle.OpenTime;

        // ML bracket trades run an ATR-scaled breakeven ratchet on every closed candle while a
        // position is open (indicator strategies have no live ratchet). This only tightens the stop;
        // the tick-driven TradeMonitorService then enforces it. Runs before the signal check, which
        // returns None while a trade is open (ML is entry-only), so the two never conflict.
        if ((TradingStrategy)tradeBot.TradingStrategyId == TradingStrategy.MlPolicy)
            await MaybeRatchetMlBreakevenAsync(dbContext, tradeBot, candle, cancellationToken);

        // Enforce the per-trade candle-age cap that the backtest and the ML training env both apply:
        // once an open trade has spanned maxCandlesPerTrade candles, force-close it at market so live
        // execution matches training/backtest (mirrors BacktestSimulationEngine's openTradeCandles
        // cap). Runs before the signal check so, like the backtest, a max-candles exit wins over a
        // same-candle opposite signal. Applies to every strategy. If SL/TP fired intra-candle the
        // trade is already Closed, so this is a no-op and never double-closes.
        if (await MaybeForceCloseMaxCandlesAsync(dbContext, tradeBot, candle, tradeService, cancellationToken))
            return;

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

        // Entry gates: a bot may only OPEN a new position when both the session window (if
        // session-only) and its per-day risk limits allow it. These mirror the backtest engine's
        // BacktestSimulationEngine.CanEnterToday so live and backtest gate entries identically. They
        // gate entries only — the exits/opposite-signal closes above still run so a position opened
        // earlier is never trapped open once the session ends or a daily limit is hit.
        if (tradeBot.IsNySessionOnly && !BacktestSimulationEngine.IsWithinNySession(candleOpenTime))
        {
            logger.LogDebug(
                "Tradebot {TradeBotId} entry suppressed: candle {CandleOpenTime:o} is outside the NY session",
                tradeBot.Id,
                candleOpenTime);
            return;
        }

        var dailyLimitReason = await DailyEntryBlockReasonAsync(dbContext, tradeBot, candleOpenTime, cancellationToken);
        if (dailyLimitReason is not null)
        {
            logger.LogDebug(
                "Tradebot {TradeBotId} entry suppressed: {Reason}",
                tradeBot.Id,
                dailyLimitReason);
            return;
        }

        // ML entries size the stop/take-profit from the policy's bracket decision and the ATR-at-entry
        // (stop = slAtrMult × ATR, TP = tpRMult × stop), capturing the ATR so the breakeven ratchet can
        // scale off it. Indicator strategies keep their fixed bot-level SL/TP. Live fills use the real
        // market price, so no synthetic slippage is applied here (unlike the backtest env model).
        var stopLoss = tradeBot.StopLoss;
        var takeProfit = tradeBot.TakeProfit;
        var quantity = tradeBot.Quantity;
        decimal? atrAtEntry = null;
        if (signal.Bracket is MlBracket bracket)
        {
            var (slDistance, tpDistance) = BacktestSimulationEngine.MlBracketDistances(
                bracket.AtrAtEntry, bracket.SlAtrMult, bracket.TpRMult);
            stopLoss = slDistance;
            takeProfit = tpDistance;
            atrAtEntry = bracket.AtrAtEntry;
            // ATR-regime policies return an explicit regime-selected quantity, applied verbatim; legacy
            // policies fall back to volatility-targeted sizing from the policy's risk-per-trade (the
            // bound bot carries no fixed quantity, so a policy without risk-per-trade sizes to 0).
            quantity = BacktestSimulationEngine.MlPositionSize(
                tradeBot.Quantity, tradeBot.MlPolicy?.RiskPerTrade, slDistance, bracket.Quantity);
        }

        try
        {
            await tradeService.CreateAsync(
                new CreateTradeRequestDto(
                    SymbolCode: tradeBot.Symbol.Code,
                    IntervalCode: tradeBot.Interval.Code,
                    Side: side,
                    OrderType: TradeOrderType.Market,
                    Quantity: quantity,
                    LimitPrice: null,
                    StopLoss: stopLoss,
                    TakeProfit: takeProfit,
                    TradingAccountId: tradeBot.TradingAccountId!.Value,
                    Fee: tradeBot.Fee,
                    AtrAtEntry: atrAtEntry),
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

    /// <summary>
    /// Arms/ratchets the ATR-scaled breakeven stop for the account's open ML trade against the just
    /// closed candle, mirroring the backtest engine's TryArmMlBreakeven. No-op when breakeven is
    /// disabled, no ML trade is open, or the trade predates ATR-at-entry capture. Only ever tightens
    /// the stop, so it is safe to run every candle without tracking an armed flag.
    /// </summary>
    private async Task MaybeRatchetMlBreakevenAsync(
        ApplicationDbContext dbContext,
        Models.TradeBot tradeBot,
        ClosedCandleEvent candle,
        CancellationToken cancellationToken)
    {
        if (tradeBot.Breakeven is not decimal breakeven || breakeven <= 0m)
            return;

        var openTrade = await dbContext.Trades
            .Where(t => t.BacktestId == null &&
                        t.TradingAccountId == tradeBot.TradingAccountId &&
                        t.StatusId == (int)TradeStatus.Active)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (openTrade?.EntryPrice is null || openTrade.StopLoss is null || openTrade.AtrAtEntry is null)
            return;

        var armed = BacktestSimulationEngine.TryArmMlBreakeven(
            openTrade, candle.High, candle.Low, breakeven, tradeBot.BreakevenStop ?? 0m);

        if (!armed)
            return;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Tradebot {TradeBotId} ratcheted ML breakeven on trade {TradeId}: stop offset now {StopLoss}",
            tradeBot.Id, openTrade.Id, openTrade.StopLoss);
    }

    /// <summary>
    /// Force-closes the account's open trade once it has spanned <c>MaxCandlesPerTrade</c> candles,
    /// mirroring the backtest/training-env candle-age cap so live trades don't outlive the horizon the
    /// model/strategy was tuned for. Returns true when it closed a trade (the caller then skips signal
    /// evaluation for this candle). No-op when the cap is unset/≤ 0 or no active trade is open. Candle
    /// age uses the same "candles since an event" convention as the rest of the live path
    /// (<see cref="CountCandlesSinceLastTradeClosedAsync"/>): candles whose open-time is after the
    /// trade's OpenedAt, which lines up with the backtest's per-trade candle counter.
    /// </summary>
    private async Task<bool> MaybeForceCloseMaxCandlesAsync(
        ApplicationDbContext dbContext,
        Models.TradeBot tradeBot,
        ClosedCandleEvent candle,
        ITradeService tradeService,
        CancellationToken cancellationToken)
    {
        if (tradeBot.MaxCandlesPerTrade is not int maxCandles || maxCandles <= 0)
            return false;

        var openTrade = await dbContext.Trades
            .Where(t => t.BacktestId == null &&
                        t.TradingAccountId == tradeBot.TradingAccountId &&
                        t.StatusId == (int)TradeStatus.Active)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (openTrade?.OpenedAt is not DateTimeOffset openedAt)
            return false;

        var candlesOpen = await dbContext.KlineData
            .CountAsync(
                k => k.SymbolId == tradeBot.SymbolId &&
                     k.IntervalId == tradeBot.IntervalId &&
                     k.OpenTime > openedAt &&
                     k.OpenTime <= candle.OpenTime,
                cancellationToken);

        if (candlesOpen < maxCandles)
            return false;

        try
        {
            await tradeService.CloseAsync(openTrade.Id, TradeCloseReason.Manual, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The tick-driven monitor closed it (SL/TP) between our read and here — nothing to do.
            return false;
        }

        logger.LogInformation(
            "Tradebot {TradeBotId} force-closed trade {TradeId} for account {AccountId}: reached max candles per trade ({CandlesOpen} >= {MaxCandles})",
            tradeBot.Id, openTrade.Id, tradeBot.TradingAccountId, candlesOpen, maxCandles);
        return true;
    }

    /// <summary>
    /// Returns a reason string when the bot has hit a per-day entry limit for the candle's Eastern
    /// day, else null. Mirrors the daily-limit half of BacktestSimulationEngine.CanEnterToday: stats
    /// are the account's realized (fee-adjusted) PnL and losing-trade count for trades that CLOSED on
    /// that Eastern day. Only queried when at least one limit is configured.
    /// </summary>
    private async Task<string?> DailyEntryBlockReasonAsync(
        ApplicationDbContext dbContext,
        Models.TradeBot tradeBot,
        DateTimeOffset candleOpenTime,
        CancellationToken cancellationToken)
    {
        if (tradeBot.DailyProfitGoal is null && tradeBot.MaxLossesPerDay is null)
            return null;

        var easternDay = BacktestSimulationEngine.EasternDay(candleOpenTime);
        // Widen the lower bound by two days so a trade closed at the very start of this Eastern day is
        // still fetched regardless of the UTC offset / DST; the exact per-trade Eastern-day match is
        // then done in memory to stay identical to the backtest's EasternDay(ClosedAt) grouping.
        var since = candleOpenTime.AddDays(-2);

        var closedToday = (await dbContext.Trades
                .AsNoTracking()
                .Where(t => t.BacktestId == null &&
                            t.TradingAccountId == tradeBot.TradingAccountId &&
                            t.StatusId == (int)TradeStatus.Closed &&
                            t.ClosedAt != null &&
                            t.ClosedAt >= since)
                .Select(t => new { t.ClosedAt, t.Pnl })
                .ToListAsync(cancellationToken))
            .Where(t => BacktestSimulationEngine.EasternDay(t.ClosedAt!.Value) == easternDay)
            .ToList();

        var dailyPnl = closedToday.Sum(t => t.Pnl ?? 0m);
        var dailyLosses = closedToday.Count(t => (t.Pnl ?? 0m) < 0);

        if (tradeBot.DailyProfitGoal is decimal goal && dailyPnl >= goal)
            return $"daily profit goal reached ({dailyPnl} >= {goal})";

        if (tradeBot.MaxLossesPerDay is int maxLosses && dailyLosses >= maxLosses)
            return $"max losses per day reached ({dailyLosses} >= {maxLosses})";

        return null;
    }
}
