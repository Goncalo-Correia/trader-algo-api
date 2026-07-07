using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Backtests;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Dtos.Ml;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Ml;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;
using TraderAlgoApi.Services.Rules.SmaMacd;

namespace TraderAlgoApi.Services.Backtests;

// Compute/replay split: the simulation runs to completion (ComputeAsync, no socket
// involvement) persisting trades and progress; the WebSocket then replays the finished
// run from the database (ReplayAsync). This removes streaming overhead from the sim
// itself, structurally guarantees candle-before-trade ordering, and makes a completed
// run re-watchable (the basis for future scrub/seek).
public sealed class BacktestStreamService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    SmaTradingRule smaTradingRule,
    RsiTradingRule rsiTradingRule,
    MacdTradingRule macdTradingRule,
    SmaMacdTradingRule smaMacdTradingRule,
    IMlConnectorService mlConnector,
    TimeProvider timeProvider,
    ILogger<BacktestStreamService> logger) : IBacktestStreamService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Serialize enums (e.g. Trade.Side) as strings so streamed trade events match the
        // client's "Buy"/"Sell" guard and the REST contract; without this the client silently
        // drops every trade frame and markers only appear after the run via REST reconciliation.
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly TimeSpan CandleInterval = TimeSpan.FromMilliseconds(100);

    // How often to flush incremental backtest progress (candle count / balance) to the DB.
    // Trade open/close already flush on their own; this only bounds the gap between them so a
    // crashed run resumes from at most this many candles back instead of hammering the DB every candle.
    private const int ProgressFlushEvery = 200;

    // Candles are coalesced into a single "candleBatch" frame outside delay mode, cutting
    // thousands of tiny per-candle frames (and their parse/render cost) down to a handful.
    private const int CandleBatchSize = 250;

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
                .ThenInclude(tb => tb!.MlPolicy)
            .FirstOrDefaultAsync(b => b.Id == backtestId, cancellationToken);

        if (backtest is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Backtest {backtestId} not found.", cancellationToken);
            return;
        }

        // Pending/Running runs are computed first; Completed runs go straight to replay.
        if ((BacktestStatus)backtest.StatusId is not (BacktestStatus.Pending or BacktestStatus.Running or BacktestStatus.Completed))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(
                $"Backtest {backtestId} cannot be streamed: status is {(BacktestStatus)backtest.StatusId}.",
                cancellationToken);
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Monitor for client-initiated close frames on a background task.
        using var disconnectCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disconnectCts.Token);
        var monitorTask = MonitorClientAsync(clientSocket, disconnectCts, linked.Token);

        var computing = false;
        try
        {
            if ((BacktestStatus)backtest.StatusId is BacktestStatus.Pending or BacktestStatus.Running)
            {
                computing = true;
                backtest.StatusId = (int)BacktestStatus.Running;
                await dbContext.SaveChangesAsync(cancellationToken);

                await ComputeAsync(dbContext, backtest, linked.Token);

                backtest.StatusId = (int)BacktestStatus.Completed;
                backtest.CompletedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(CancellationToken.None);
                computing = false;
            }

            await ReplayAsync(dbContext, clientSocket, backtest, delay, linked.Token);

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
            logger.LogInformation("Backtest {Id} stream stopped: client disconnected", backtest.Id);
            if (computing)
            {
                backtest.StatusId = (int)BacktestStatus.Cancelled;
                backtest.CompletedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            if (computing)
            {
                backtest.StatusId = (int)BacktestStatus.Cancelled;
                backtest.CompletedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backtest {Id} stream failed", backtest.Id);
            if (computing)
            {
                backtest.StatusId = (int)BacktestStatus.Failed;
                backtest.CompletedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
        finally
        {
            await monitorTask;
            await DisableLinkedTradeBotAsync(dbContext, backtest.Id);
        }
    }

    // -------------------------------------------------------------------------
    // Compute: run the simulation to completion, persisting trades and progress.
    // No socket involvement — a fast run no longer pays any streaming overhead.
    // -------------------------------------------------------------------------

    private async Task ComputeAsync(
        ApplicationDbContext dbContext,
        Backtest backtest,
        CancellationToken cancellationToken)
    {
        var bot = backtest.TradeBot!;
        var rule = SelectRule(bot.TradingStrategyId);
        if ((TradingStrategy)bot.TradingStrategyId == TradingStrategy.MlPolicy && bot.MlPolicy is null)
            throw new InvalidOperationException($"Backtest {backtest.Id} uses ML Policy but its tradebot has no linked ML policy.");

        var rangeCandles = await LoadRangeCandlesAsync(dbContext, backtest, bot.IsNySessionOnly, cancellationToken);

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
            .Include(k => k.Atr)
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
        var closedTradeHistory = closedTrades
            .OrderBy(t => t.ClosedAt ?? t.CreatedAt)
            .ToList();
        var lastClosedTrade = closedTradeHistory.LastOrDefault(t => t.ClosedAt.HasValue);
        int? lastClosedCandleIndex = null;
        if (lastClosedTrade?.ClosedAt is DateTimeOffset lastClosedAt)
        {
            var index = combined.FindLastIndex(k => k.OpenTime <= lastClosedAt);
            if (index >= 0)
                lastClosedCandleIndex = index;
        }

        // Skip already-computed candles; always need at least 2 items before current.
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
                    closedTradeHistory.Add(openTrade);
                    lastClosedCandleIndex = i;
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
                closedTradeHistory.Add(openTrade);
                lastClosedCandleIndex = i;
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
                    side = await DecideViaMlAsync(
                        backtest,
                        bot,
                        context,
                        current,
                        balance,
                        dailyStats,
                        closedTradeHistory,
                        lastClosedCandleIndex,
                        i,
                        cancellationToken);
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
    // Replay: stream a finished run from the database. Candles are emitted in
    // order with the trades interleaved at their persisted open/close times, so
    // a trade event can only ever reference a bar the client already holds.
    // -------------------------------------------------------------------------

    private async Task ReplayAsync(
        ApplicationDbContext dbContext,
        WebSocket clientSocket,
        Backtest backtest,
        bool delay,
        CancellationToken cancellationToken)
    {
        var isNySessionOnly = backtest.TradeBot?.IsNySessionOnly ?? false;
        var rangeCandles = await LoadRangeCandlesAsync(dbContext, backtest, isNySessionOnly, cancellationToken);

        var symbolCode   = backtest.Symbol.Code;
        var intervalCode = backtest.Interval.Code;

        var trades = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.BacktestId == backtest.Id)
            .ToListAsync(cancellationToken);

        // Note: breakeven bracket updates are not replayed — their timing is not
        // persisted, so a replayed tradeOpened simply carries the trade's final
        // stop loss. Markers and PnL are unaffected.
        var pendingOpens = new Queue<Trade>(
            trades.Where(t => t.OpenedAt.HasValue).OrderBy(t => t.OpenedAt));
        var pendingCloses = new Queue<Trade>(
            trades.Where(t => t.ClosedAt.HasValue).OrderBy(t => t.ClosedAt));

        var candleBuffer = new List<CandleWithIndicatorsResponseDto>(CandleBatchSize);

        async Task FlushCandlesAsync()
        {
            if (candleBuffer.Count == 0)
                return;
            await SendMessageAsync(clientSocket, "candleBatch", candleBuffer, cancellationToken);
            candleBuffer.Clear();
        }

        foreach (var candle in rangeCandles)
        {
            if (delay)
                await Task.Delay(CandleInterval, cancellationToken);

            candleBuffer.Add(ToDto(candle));
            if (delay)
                await FlushCandlesAsync();

            while (pendingOpens.Count > 0 && pendingOpens.Peek().OpenedAt!.Value <= candle.OpenTime)
            {
                await FlushCandlesAsync();
                await SendMessageAsync(clientSocket, "tradeOpened",
                    ToTradeDto(pendingOpens.Dequeue(), symbolCode, intervalCode), cancellationToken);
            }

            while (pendingCloses.Count > 0 && pendingCloses.Peek().ClosedAt!.Value <= candle.OpenTime)
            {
                await FlushCandlesAsync();
                await SendMessageAsync(clientSocket, "tradeClosed",
                    ToTradeDto(pendingCloses.Dequeue(), symbolCode, intervalCode), cancellationToken);
            }

            // Outside delay mode the buffer is paced by size rather than time.
            if (candleBuffer.Count >= CandleBatchSize)
                await FlushCandlesAsync();
        }

        await FlushCandlesAsync();

        // Force-closed trades carry the final candle's CloseTime, which lands after the
        // last OpenTime — emit anything still pending once all candles are out.
        while (pendingCloses.Count > 0)
        {
            await SendMessageAsync(clientSocket, "tradeClosed",
                ToTradeDto(pendingCloses.Dequeue(), symbolCode, intervalCode), cancellationToken);
        }
    }

    private static async Task<List<KlineData>> LoadRangeCandlesAsync(
        ApplicationDbContext dbContext,
        Backtest backtest,
        bool isNySessionOnly,
        CancellationToken cancellationToken)
    {
        var rangeCandles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == backtest.SymbolId &&
                        k.IntervalId == backtest.IntervalId &&
                        k.OpenTime >= backtest.From &&
                        k.OpenTime <= backtest.To)
            .Include(k => k.SimpleMovingAverage)
            .Include(k => k.RelativeStrengthIndex)
            .Include(k => k.Macd)
            .Include(k => k.Atr)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        return isNySessionOnly
            ? rangeCandles.Where(BacktestSimulationEngine.IsNySessionCandle).ToList()
            : rangeCandles;
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

    private static (int Wins, int Losses) CountStreaks(IReadOnlyList<Trade> closedTrades)
    {
        if (closedTrades.Count == 0 || closedTrades[^1].Pnl is null or 0m)
            return (0, 0);

        var count = 0;
        var winning = closedTrades[^1].Pnl > 0m;
        for (var i = closedTrades.Count - 1; i >= 0; i--)
        {
            var pnl = closedTrades[i].Pnl;
            if (pnl is null)
                break;

            if (winning && pnl > 0m)
                count++;
            else if (!winning && pnl < 0m)
                count++;
            else
                break;
        }

        return winning ? (count, 0) : (0, count);
    }

    private static string CloseReasonName(Trade? trade) =>
        trade?.CloseReasonId switch
        {
            var id when id == (int)TradeCloseReason.StopLoss => "stop_loss",
            var id when id == (int)TradeCloseReason.TakeProfit => "take_profit",
            var id when id == (int)TradeCloseReason.BotSignal => "bot_signal",
            var id when id == (int)TradeCloseReason.Manual => "manual",
            _ => string.Empty
        };

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
        TradeBot bot,
        TradingRuleContext context,
        KlineData current,
        decimal balance,
        IReadOnlyDictionary<DateOnly, (decimal Pnl, int Losses)> dailyStats,
        IReadOnlyList<Trade> closedTradeHistory,
        int? lastClosedCandleIndex,
        int currentCandleIndex,
        CancellationToken cancellationToken)
    {
        var policy = bot.MlPolicy
            ?? throw new InvalidOperationException($"Tradebot {bot.Id} uses ML Policy but has no linked ML policy.");

        var day = BacktestSimulationEngine.EasternDay(current.OpenTime);
        dailyStats.TryGetValue(day, out var todayStats);
        var currentDailyDrawdown = Math.Max(0m, -todayStats.Pnl);
        var (winsInRow, lossesInRow) = CountStreaks(closedTradeHistory);
        var lastTrade = closedTradeHistory.LastOrDefault(t => t.ClosedAt.HasValue);
        var candlesSinceLastTradeClosed = lastClosedCandleIndex.HasValue
            ? currentCandleIndex - lastClosedCandleIndex.Value
            : 0;

        var request = new MlDecideRequest(
            MlPolicyId: policy.Id,
            Symbol:   backtest.Symbol.Code,
            Interval: backtest.Interval.Code,
            ModelId:  policy.Id.ToString(),
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
            InitialAccountBalance: backtest.InitialBalance,
            CurrentAccountBalance: balance,
            CurrentDailyPnl: todayStats.Pnl,
            CurrentDailyDrawdown: currentDailyDrawdown,
            WinsInRow: winsInRow,
            LossesInRow: lossesInRow,
            TradesTakenToday: closedTradeHistory.Count(
                t => t.OpenedAt.HasValue && BacktestSimulationEngine.EasternDay(t.OpenedAt.Value) == day),
            DailyProfitTargetReached: policy.DailyProfit > 0m && todayStats.Pnl >= policy.DailyProfit,
            DailyDrawdownReached: policy.DailyDrawdownLimit > 0m && currentDailyDrawdown >= policy.DailyDrawdownLimit,
            LastTradePnl: lastTrade?.Pnl ?? 0m,
            LastTradeCloseReason: CloseReasonName(lastTrade),
            CandlesSinceLastTradeClosed: candlesSinceLastTradeClosed,
            ConfiguredStopLoss: policy.StopLoss,
            ConfiguredTakeProfit: policy.TakeProfit,
            ConfiguredBreakeven: policy.Breakeven,
            ConfiguredBreakevenStop: policy.BreakevenStop,
            ConfiguredMaxCandlesPerTrade: policy.MaxCandlesPerTrade,
            FeeRate: policy.Fee,
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
            k.Macd?.Histogram,
            k.Atr?.Period,
            k.Atr?.TrueRange,
            k.Atr?.AtrValue);

    // Serializes one typed envelope and writes it as a single WebSocket text frame.
    private static async Task SendMessageAsync<T>(
        WebSocket socket,
        string type,
        T data,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new BacktestStreamMessageDto<T>(type, data),
            JsonOptions);
        await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    // Projects a simulated trade to the shared wire DTO. The backtest already holds the
    // symbol/interval codes, so we avoid loading the trade's navigation properties.
    private static TradeResponseDto ToTradeDto(Trade t, string symbolCode, string? intervalCode) =>
        new(
            Id:               t.Id,
            SymbolCode:       symbolCode,
            IntervalCode:     intervalCode,
            Side:             (TradeSide)t.SideId,
            OrderType:        (TradeOrderType)t.OrderTypeId,
            Quantity:         t.Quantity,
            RequestedPrice:   t.RequestedPrice,
            EntryPrice:       t.EntryPrice,
            StopLoss:         t.StopLoss,
            TakeProfit:       t.TakeProfit,
            Status:           (TradeStatus)t.StatusId,
            CreatedAt:        t.CreatedAt.ToUnixTimeMilliseconds(),
            OpenedAt:         t.OpenedAt?.ToUnixTimeMilliseconds(),
            ClosedAt:         t.ClosedAt?.ToUnixTimeMilliseconds(),
            ClosedPrice:      t.ClosedPrice,
            CloseReason:      t.CloseReasonId is int id ? (TradeCloseReason)id : null,
            Fee:              t.Fee,
            Pnl:              t.Pnl,
            AccountPnl:       t.AccountPnl,
            UnrealizedPnl:    null,
            TradingAccountId: t.TradingAccountId,
            BacktestId:       t.BacktestId);

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
