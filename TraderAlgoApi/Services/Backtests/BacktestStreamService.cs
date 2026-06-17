using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Backtests;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Dtos.Ml;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Ml;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;
using TraderAlgoApi.Services.Rules.SmaMacd;

namespace TraderAlgoApi.Services.Backtests;

public sealed class BacktestStreamService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    SmaTradingRule smaTradingRule,
    RsiTradingRule rsiTradingRule,
    MacdTradingRule macdTradingRule,
    SmaMacdTradingRule smaMacdTradingRule,
    IMlConnectorService mlConnector,
    MlConnectorOptions mlOptions,
    TimeProvider timeProvider,
    ILogger<BacktestStreamService> logger) : IBacktestStreamService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CandleInterval = TimeSpan.FromMilliseconds(100);

    // How often to flush incremental backtest progress (candle count / balance) to the DB.
    // Trade open/close already flush on their own; this only bounds the gap between them so a
    // crashed run resumes from at most this many candles back instead of hammering the DB every candle.
    private const int ProgressFlushEvery = 200;

    public async Task StreamAsync(
        HttpContext context,
        long backtestId,
        bool delay = false,
        CancellationToken cancellationToken = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsync(
                "This endpoint requires a WebSocket connection.",
                cancellationToken);
            return;
        }

        // Own a context for the lifetime of this run instead of piggybacking the request scope's
        // shared, long-lived DbContext.
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var backtest = await dbContext.Backtests
            .Include(b => b.Symbol)
            .Include(b => b.Interval)
            .Include(b => b.TradeBot)
            .FirstOrDefaultAsync(b => b.Id == backtestId, cancellationToken);

        if (backtest is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Backtest {backtestId} not found.", cancellationToken);
            return;
        }

        if ((BacktestStatus)backtest.StatusId is not (BacktestStatus.Pending or BacktestStatus.Running))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(
                $"Backtest {backtestId} cannot be started: status is {(BacktestStatus)backtest.StatusId}.",
                cancellationToken);
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Monitor for client-initiated close frames on a background task.
        using var disconnectCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disconnectCts.Token);
        var monitorTask = MonitorClientAsync(clientSocket, disconnectCts, linked.Token);

        // Transition Pending → Running.
        backtest.StatusId = (int)BacktestStatus.Running;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await RunSimulationAsync(dbContext, clientSocket, backtest, delay, linked.Token);

            backtest.StatusId = (int)BacktestStatus.Completed;
            backtest.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(CancellationToken.None);

            if (clientSocket.State == WebSocketState.Open)
            {
                await clientSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Backtest completed.",
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (disconnectCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Backtest {Id} cancelled: client disconnected", backtest.Id);
            backtest.StatusId = (int)BacktestStatus.Cancelled;
            backtest.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            backtest.StatusId = (int)BacktestStatus.Cancelled;
            backtest.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backtest {Id} stream failed", backtest.Id);
            backtest.StatusId = (int)BacktestStatus.Failed;
            backtest.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            await monitorTask;
            await DisableLinkedTradeBotAsync(dbContext, backtest.Id);
        }
    }

    // -------------------------------------------------------------------------
    // Simulation
    // -------------------------------------------------------------------------

    private async Task RunSimulationAsync(
        ApplicationDbContext dbContext,
        WebSocket clientSocket,
        Backtest backtest,
        bool delay,
        CancellationToken cancellationToken)
    {
        var bot = backtest.TradeBot!;
        var rule = SelectRule(bot.TradingStrategyId);

        var rangeCandles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == backtest.SymbolId &&
                        k.IntervalId == backtest.IntervalId &&
                        k.OpenTime >= backtest.From &&
                        k.OpenTime <= backtest.To)
            .Include(k => k.SimpleMovingAverage)
            .Include(k => k.RelativeStrengthIndex)
            .Include(k => k.Macd)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        if (bot.IsNySessionOnly)
            rangeCandles = rangeCandles.Where(BacktestSimulationEngine.IsNySessionCandle).ToList();

        // When NY-session-only, fetch enough prior candles to find 2 that fall within session hours.
        var priorLookback = bot.IsNySessionOnly ? 20 : 2;
        var priorCandlesRaw = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == backtest.SymbolId &&
                        k.IntervalId == backtest.IntervalId &&
                        k.OpenTime < backtest.From)
            .Include(k => k.SimpleMovingAverage)
            .Include(k => k.RelativeStrengthIndex)
            .Include(k => k.Macd)
            .OrderByDescending(k => k.OpenTime)
            .Take(priorLookback)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        var priorCandles = bot.IsNySessionOnly
            ? priorCandlesRaw.Where(BacktestSimulationEngine.IsNySessionCandle).TakeLast(2).ToList()
            : priorCandlesRaw;

        // [prior0, prior1, range0, range1, ...]
        var combined = priorCandles.Concat(rangeCandles).ToList();

        // Resume state: balance and open trade from a previous partial run.
        var balance = backtest.FinalBalance ?? backtest.InitialBalance;
        var openTrade = await dbContext.Trades
            .Where(t => t.BacktestId == backtest.Id && t.StatusId == (int)TradeStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);

        // Rebuild daily stats from trades already closed in a previous partial run.
        var closedTrades = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.BacktestId == backtest.Id && t.StatusId == (int)TradeStatus.Closed && t.ClosedAt.HasValue)
            .ToListAsync(cancellationToken);

        var dailyStats = closedTrades
            .GroupBy(t => BacktestSimulationEngine.EasternDay(t.ClosedAt!.Value))
            .ToDictionary(
                g => g.Key,
                g => (Pnl: g.Sum(t => t.Pnl ?? 0m), Losses: g.Count(t => (t.Pnl ?? 0m) < 0)));

        // Skip already-emitted candles; always need at least 2 items before current.
        var startIndex = Math.Max(2, priorCandles.Count + backtest.CandleCount);

        // Restore candle-age of a resumed open trade so MaxCandlesPerTrade still fires correctly.
        var openTradeCandles = 0;
        if (openTrade is not null && bot.MaxCandlesPerTrade.HasValue && openTrade.OpenedAt.HasValue)
        {
            openTradeCandles = rangeCandles.Count(
                k => k.OpenTime >= openTrade.OpenedAt.Value &&
                     k.OpenTime < combined[startIndex].OpenTime);
        }

        var candlesSinceFlush = 0;

        for (var i = startIndex; i < combined.Count; i++)
        {
            var current      = combined[i];
            var previous     = combined[i - 1];
            var secondPrev   = combined[i - 2];

            var context = BacktestSimulationEngine.BuildContext(
                backtest.Symbol.Code, backtest.Interval.Code,
                secondPrev, previous, current);

            // Advance per-trade candle counter.
            if (openTrade is not null)
                openTradeCandles++;

            // Check SL/TP on any open trade before evaluating signals.
            if (openTrade is not null)
            {
                var (closeReason, closePrice) = BacktestSimulationEngine.CheckSlTp(openTrade, current);
                if (closeReason.HasValue)
                {
                    BacktestSimulationEngine.CloseTrade(openTrade, closePrice!.Value, closeReason.Value, current.OpenTime, ref balance);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    ApplyDailyStat(dailyStats, openTrade);
                    openTrade = null;
                    openTradeCandles = 0;
                }
            }

            // Close by candle age when SL/TP didn't fire first.
            if (openTrade is not null &&
                bot.MaxCandlesPerTrade.HasValue &&
                openTradeCandles >= bot.MaxCandlesPerTrade.Value)
            {
                BacktestSimulationEngine.CloseTrade(openTrade, current.Close, TradeCloseReason.Manual, current.OpenTime, ref balance);
                await dbContext.SaveChangesAsync(cancellationToken);

                ApplyDailyStat(dailyStats, openTrade);
                openTrade = null;
                openTradeCandles = 0;
            }

            // Check breakeven trigger on the surviving open trade.
            if (openTrade is not null && bot.Breakeven.HasValue && openTrade.StopLoss != 0)
            {
                var breakevenTriggered = BacktestSimulationEngine.CheckBreakeven(openTrade, current, bot.Breakeven.Value);
                if (breakevenTriggered)
                {
                    openTrade.StopLoss = bot.BreakevenStop.HasValue ? -bot.BreakevenStop.Value : 0m;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    var bracketUpdate = new TradeBracketUpdateDto(
                        TradeId:    openTrade.Id,
                        StopLoss:   openTrade.StopLoss,
                        TakeProfit: openTrade.TakeProfit);

                    var bracketPayload = JsonSerializer.SerializeToUtf8Bytes(
                        new BacktestStreamMessageDto<TradeBracketUpdateDto>("tradeBracketUpdate", bracketUpdate),
                        JsonOptions);

                    await clientSocket.SendAsync(
                        bracketPayload,
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken);
                }
            }

            // Evaluate entry signal when flat.
            if (openTrade is null && BacktestSimulationEngine.CanEnterToday(bot, current.OpenTime, dailyStats))
            {
                TradeSide? side = null;

                if (rule is not null)
                {
                    if (rule.ShouldEnterLong(context))
                        side = TradeSide.Buy;
                    else if (rule.ShouldEnterShort(context))
                        side = TradeSide.Sell;
                }
                else
                {
                    // MlPolicy: ask the sidecar for a decision
                    side = await DecideViaMlAsync(backtest, context, current, cancellationToken);
                }

                if (side.HasValue)
                {
                    openTrade = new Trade
                    {
                        SymbolId         = backtest.SymbolId,
                        IntervalId       = backtest.IntervalId,
                        SideId           = (int)side.Value,
                        OrderTypeId      = (int)TradeOrderType.Market,
                        Quantity         = bot.Quantity,
                        EntryPrice       = current.Close,
                        StopLoss         = bot.StopLoss,
                        TakeProfit       = bot.TakeProfit,
                        StatusId         = (int)TradeStatus.Active,
                        CreatedAt        = current.OpenTime,
                        OpenedAt         = current.OpenTime,
                        Fee              = bot.Fee,
                        TradingAccountId = null,
                        BacktestId       = backtest.Id
                    };
                    dbContext.Trades.Add(openTrade);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    openTradeCandles = 1;
                }
            }

            if (delay)
                await Task.Delay(CandleInterval, cancellationToken);

            // Send candle to client.
            var candleDto = ToDto(current);
            var payload = JsonSerializer.SerializeToUtf8Bytes(
                new BacktestStreamMessageDto<CandleWithIndicatorsResponseDto>("candle", candleDto),
                JsonOptions);
            await clientSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            // Track incremental progress in memory; flush periodically so REST endpoints reflect
            // current state without a DB round-trip on every single candle.
            backtest.CandleCount++;
            backtest.FinalBalance = balance;
            backtest.Pnl = balance - backtest.InitialBalance;

            if (++candlesSinceFlush >= ProgressFlushEvery)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                candlesSinceFlush = 0;
            }
        }

        // Force-close any open trade at the final candle's close price.
        if (openTrade is not null && rangeCandles.Count > 0)
        {
            var last = rangeCandles[^1];
            BacktestSimulationEngine.CloseTrade(openTrade, last.Close, TradeCloseReason.Manual, last.CloseTime, ref balance);
            backtest.FinalBalance = balance;
            backtest.Pnl = balance - backtest.InitialBalance;
        }

        // Final flush of any progress accumulated since the last batch.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void ApplyDailyStat(
        Dictionary<DateOnly, (decimal Pnl, int Losses)> dailyStats,
        Trade closedTrade)
    {
        var closeDay = BacktestSimulationEngine.EasternDay(closedTrade.ClosedAt!.Value);
        if (!dailyStats.TryGetValue(closeDay, out var stats))
            stats = (0m, 0);
        dailyStats[closeDay] = (
            stats.Pnl + (closedTrade.Pnl ?? 0m),
            stats.Losses + ((closedTrade.Pnl ?? 0m) < 0 ? 1 : 0));
    }

    private static async Task DisableLinkedTradeBotAsync(ApplicationDbContext dbContext, long backtestId)
    {
        await dbContext.TradeBots
            .Where(b => b.BacktestId == backtestId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.IsEnabled, false),
                CancellationToken.None);
    }

    private async Task<TradeSide?> DecideViaMlAsync(
        Backtest backtest,
        TradingRuleContext context,
        KlineData current,
        CancellationToken cancellationToken)
    {
        var request = new MlDecideRequest(
            Symbol:   backtest.Symbol.Code,
            Interval: backtest.Interval.Code,
            ModelId:  mlOptions.ModelId,
            Candle: new MlCandleFeatures(
                Open:           context.CurrentOpen,
                High:           context.CurrentHigh,
                Low:            context.CurrentLow,
                Close:          context.CurrentClose,
                Volume:         current.Volume,
                TakerBuyVolume: current.TakerBuyBaseAssetVolume,
                Sma20:          context.CurrentSma20,
                Sma100:         context.CurrentSma100,
                Rsi:            context.CurrentRsi,
                RsiSmooth:      context.CurrentRsiSmooth,
                MacdLine:       context.CurrentMacdLine,
                SignalLine:     context.CurrentSignalLine,
                Histogram:      context.CurrentHistogram),
            Position:      0,
            CandlesHeld:   0,
            UnrealizedPnl: 0m);

        var response = await mlConnector.DecideAsync(request, cancellationToken);

        return response.Action switch
        {
            1 => TradeSide.Buy,   // EnterLong
            2 => TradeSide.Sell,  // EnterShort
            _ => null             // Hold or Close
        };
    }

    private ITradingRule? SelectRule(int strategyId) => (TradingStrategy)strategyId switch
    {
        TradingStrategy.Sma      => smaTradingRule,
        TradingStrategy.Rsi      => rsiTradingRule,
        TradingStrategy.Macd     => macdTradingRule,
        TradingStrategy.SmaMacd  => smaMacdTradingRule,
        TradingStrategy.MlPolicy => null, // handled by mlConnector in the simulation loop
        _ => throw new ArgumentException($"Unknown strategy id {strategyId}.")
    };

    private static CandleWithIndicatorsResponseDto ToDto(KlineData k) =>
        new(
            k.OpenTime.ToUnixTimeSeconds(),
            k.Open, k.High, k.Low, k.Close, k.Volume,
            k.TakerBuyBaseAssetVolume,
            k.Volume - k.TakerBuyBaseAssetVolume,
            k.SimpleMovingAverage?.Sma20,
            k.SimpleMovingAverage?.Sma100,
            k.RelativeStrengthIndex?.Rsi,
            k.RelativeStrengthIndex?.RsiSmooth,
            k.RelativeStrengthIndex?.Divergence,
            k.Macd?.MacdLine,
            k.Macd?.SignalLine,
            k.Macd?.Histogram);

    // Reads incoming frames so the server detects client close frames.
    private static async Task MonitorClientAsync(
        WebSocket socket,
        CancellationTokenSource disconnectCts,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await disconnectCts.CancelAsync();
                    break;
                }
            }
        }
        catch
        {
            await disconnectCts.CancelAsync();
        }
    }
}
