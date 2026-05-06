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
    ApplicationDbContext dbContext,
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
    private static readonly TimeZoneInfo EasternZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly TimeOnly NyOpen  = new(9, 30);
    private static readonly TimeOnly NyClose = new(16, 0);

    public async Task StreamAsync(
        HttpContext context,
        long backtestId,
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

        var backtest = await dbContext.Backtests
            .Include(b => b.Symbol)
            .Include(b => b.Interval)
            .Include(b => b.TradingStrategy)
            .FirstOrDefaultAsync(b => b.Id == backtestId, cancellationToken);

        if (backtest is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Backtest {backtestId} not found.", cancellationToken);
            return;
        }

        if (backtest.Status is not (BacktestStatus.Pending or BacktestStatus.Running))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(
                $"Backtest {backtestId} cannot be started: status is {backtest.Status}.",
                cancellationToken);
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Monitor for client-initiated close frames on a background task.
        using var disconnectCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disconnectCts.Token);
        var monitorTask = MonitorClientAsync(clientSocket, disconnectCts, linked.Token);

        // Transition Pending → Running.
        backtest.Status = BacktestStatus.Running;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await RunSimulationAsync(clientSocket, backtest, linked.Token);

            backtest.Status = BacktestStatus.Completed;
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
            backtest.Status = BacktestStatus.Cancelled;
            backtest.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            backtest.Status = BacktestStatus.Cancelled;
            backtest.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backtest {Id} stream failed", backtest.Id);
            backtest.Status = BacktestStatus.Failed;
            backtest.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            await monitorTask;
            await DeleteLinkedTradeBotAsync(backtest.Id);
        }
    }

    // -------------------------------------------------------------------------
    // Simulation
    // -------------------------------------------------------------------------

    private async Task RunSimulationAsync(
        WebSocket clientSocket,
        Backtest backtest,
        CancellationToken cancellationToken)
    {
        var rule = SelectRule(backtest.TradingStrategyId);

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

        if (backtest.IsNySessionOnly)
            rangeCandles = rangeCandles.Where(IsNySessionCandle).ToList();

        // When NY-session-only, fetch enough prior candles to find 2 that fall within session hours.
        var priorLookback = backtest.IsNySessionOnly ? 20 : 2;
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

        var priorCandles = backtest.IsNySessionOnly
            ? priorCandlesRaw.Where(IsNySessionCandle).TakeLast(2).ToList()
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
            .GroupBy(t => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(t.ClosedAt!.Value, EasternZone).DateTime))
            .ToDictionary(
                g => g.Key,
                g => (Pnl: g.Sum(t => t.Pnl ?? 0m), Losses: g.Count(t => (t.Pnl ?? 0m) < 0)));

        // Skip already-emitted candles; always need at least 2 items before current.
        var startIndex = Math.Max(2, priorCandles.Count + backtest.CandleCount);

        // Restore candle-age of a resumed open trade so MaxCandlesPerTrade still fires correctly.
        var openTradeCandles = 0;
        if (openTrade is not null && backtest.MaxCandlesPerTrade.HasValue && openTrade.OpenedAt.HasValue)
        {
            openTradeCandles = rangeCandles.Count(
                k => k.OpenTime >= openTrade.OpenedAt.Value &&
                     k.OpenTime < combined[startIndex].OpenTime);
        }

        for (var i = startIndex; i < combined.Count; i++)
        {
            var current      = combined[i];
            var previous     = combined[i - 1];
            var secondPrev   = combined[i - 2];

            var context = BuildContext(
                backtest.Symbol.Code, backtest.Interval.Code,
                secondPrev, previous, current);

            // Advance per-trade candle counter.
            if (openTrade is not null)
                openTradeCandles++;

            // Check SL/TP on any open trade before evaluating signals.
            if (openTrade is not null)
            {
                var (closeReason, closePrice) = CheckSlTp(openTrade, current);
                if (closeReason.HasValue)
                {
                    CloseTrade(openTrade, closePrice!.Value, closeReason.Value, current.OpenTime, ref balance);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // Update daily stats for the newly closed trade.
                    var closeDay = DateOnly.FromDateTime(
                        TimeZoneInfo.ConvertTime(openTrade.ClosedAt!.Value, EasternZone).DateTime);
                    if (!dailyStats.TryGetValue(closeDay, out var stats))
                        stats = (0m, 0);
                    dailyStats[closeDay] = (
                        stats.Pnl + (openTrade.Pnl ?? 0m),
                        stats.Losses + ((openTrade.Pnl ?? 0m) < 0 ? 1 : 0));

                    openTrade = null;
                    openTradeCandles = 0;
                }
            }

            // Close by candle age when SL/TP didn't fire first.
            if (openTrade is not null &&
                backtest.MaxCandlesPerTrade.HasValue &&
                openTradeCandles >= backtest.MaxCandlesPerTrade.Value)
            {
                CloseTrade(openTrade, current.Close, TradeCloseReason.Manual, current.OpenTime, ref balance);
                await dbContext.SaveChangesAsync(cancellationToken);

                var closeDay = DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTime(openTrade.ClosedAt!.Value, EasternZone).DateTime);
                if (!dailyStats.TryGetValue(closeDay, out var dayStats))
                    dayStats = (0m, 0);
                dailyStats[closeDay] = (
                    dayStats.Pnl + (openTrade.Pnl ?? 0m),
                    dayStats.Losses + ((openTrade.Pnl ?? 0m) < 0 ? 1 : 0));

                openTrade = null;
                openTradeCandles = 0;
            }

            // Check breakeven trigger on the surviving open trade.
            if (openTrade is not null && backtest.Breakeven.HasValue && openTrade.StopLoss != 0)
            {
                var breakevenTriggered = CheckBreakeven(openTrade, current, backtest.Breakeven.Value);
                if (breakevenTriggered)
                {
                    openTrade.StopLoss = 0;
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
            if (openTrade is null && CanEnterToday(backtest, current.OpenTime, dailyStats))
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
                        Quantity         = backtest.Quantity,
                        EntryPrice       = current.Close,
                        StopLoss         = backtest.StopLoss,
                        TakeProfit       = backtest.TakeProfit,
                        StatusId         = (int)TradeStatus.Active,
                        CreatedAt        = current.OpenTime,
                        OpenedAt         = current.OpenTime,
                        TradingAccountId = null,
                        BacktestId       = backtest.Id
                    };
                    dbContext.Trades.Add(openTrade);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    openTradeCandles = 1;
                }
            }

            // Pace the stream: one candle every 0.1 seconds.
            await Task.Delay(CandleInterval, cancellationToken);

            // Send candle to client.
            var candleDto = ToDto(current);
            var payload = JsonSerializer.SerializeToUtf8Bytes(
                new BacktestStreamMessageDto<CandleWithIndicatorsResponseDto>("candle", candleDto),
                JsonOptions);
            await clientSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            // Persist incremental progress so REST endpoints reflect current state.
            backtest.CandleCount++;
            backtest.FinalBalance = balance;
            backtest.Pnl = balance - backtest.InitialBalance;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Force-close any open trade at the final candle's close price.
        if (openTrade is not null && rangeCandles.Count > 0)
        {
            var last = rangeCandles[^1];
            CloseTrade(openTrade, last.Close, TradeCloseReason.Manual, last.CloseTime, ref balance);
            backtest.FinalBalance = balance;
            backtest.Pnl = balance - backtest.InitialBalance;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task DeleteLinkedTradeBotAsync(long backtestId)
    {
        await dbContext.TradeBots
            .Where(b => b.BacktestId == backtestId)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    private static bool IsNySessionCandle(KlineData k)
    {
        var eastern = TimeZoneInfo.ConvertTime(k.OpenTime, EasternZone);
        if (eastern.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;
        var tod = TimeOnly.FromDateTime(eastern.DateTime);
        return tod >= NyOpen && tod < NyClose;
    }

    private static bool CanEnterToday(
        Backtest backtest,
        DateTimeOffset candleTime,
        Dictionary<DateOnly, (decimal Pnl, int Losses)> dailyStats)
    {
        var easternTime = TimeZoneInfo.ConvertTime(candleTime, EasternZone);

        if (backtest.IsNySessionOnly)
        {
            var dow = easternTime.DayOfWeek;
            if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday)
                return false;

            var tod = TimeOnly.FromDateTime(easternTime.DateTime);
            if (tod < NyOpen || tod >= NyClose)
                return false;
        }

        if (backtest.DailyProfitGoal.HasValue || backtest.MaxLossesPerDay.HasValue)
        {
            var day = DateOnly.FromDateTime(easternTime.DateTime);
            dailyStats.TryGetValue(day, out var stats);

            if (backtest.DailyProfitGoal.HasValue && stats.Pnl >= backtest.DailyProfitGoal.Value)
                return false;

            if (backtest.MaxLossesPerDay.HasValue && stats.Losses >= backtest.MaxLossesPerDay.Value)
                return false;
        }

        return true;
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

    private ITradingRule? SelectRule(int strategyId) => strategyId switch
    {
        1 => smaTradingRule,
        2 => rsiTradingRule,
        3 => macdTradingRule,
        4 => smaMacdTradingRule,
        5 => null, // MlPolicy: handled by mlConnector in the simulation loop
        _ => throw new ArgumentException($"Unknown strategy id {strategyId}.")
    };

    private static TradingRuleContext BuildContext(
        string symbolCode,
        string intervalCode,
        KlineData secondPrevious,
        KlineData previous,
        KlineData current) =>
        new(
            SymbolCode:          symbolCode,
            IntervalCode:        intervalCode,
            CurrentOpen:         current.Open,
            CurrentHigh:         current.High,
            CurrentLow:          current.Low,
            CurrentClose:        current.Close,
            PreviousClose:       previous.Close,
            SecondPreviousClose: secondPrevious.Close,
            CurrentSma20:        current.SimpleMovingAverage?.Sma20,
            CurrentSma100:       current.SimpleMovingAverage?.Sma100,
            PreviousSma20:       previous.SimpleMovingAverage?.Sma20,
            PreviousSma100:      previous.SimpleMovingAverage?.Sma100,
            SecondPreviousSma20: secondPrevious.SimpleMovingAverage?.Sma20,
            CurrentRsi:          current.RelativeStrengthIndex?.Rsi,
            CurrentRsiSmooth:    current.RelativeStrengthIndex?.RsiSmooth,
            PreviousRsi:         previous.RelativeStrengthIndex?.Rsi,
            PreviousRsiSmooth:   previous.RelativeStrengthIndex?.RsiSmooth,
            CurrentMacdLine:     current.Macd?.MacdLine,
            CurrentSignalLine:   current.Macd?.SignalLine,
            CurrentHistogram:    current.Macd?.Histogram,
            PreviousHistogram:   previous.Macd?.Histogram);

    private static bool CheckBreakeven(Trade trade, KlineData candle, decimal breakevenThreshold)
    {
        var entry  = trade.EntryPrice!.Value;
        var isBuy  = trade.SideId == (int)TradeSide.Buy;
        var peakPnl = isBuy
            ? (candle.High - entry) * trade.Quantity
            : (entry - candle.Low)  * trade.Quantity;

        return peakPnl >= breakevenThreshold;
    }

    private static (TradeCloseReason? Reason, decimal? Price) CheckSlTp(Trade trade, KlineData candle)
    {
        var entry = trade.EntryPrice!.Value;
        var isBuy = trade.SideId == (int)TradeSide.Buy;

        // SL takes priority when both are hit intra-candle (conservative).
        if (trade.StopLoss.HasValue)
        {
            var slPrice = isBuy ? entry - trade.StopLoss.Value : entry + trade.StopLoss.Value;
            if (isBuy ? candle.Low <= slPrice : candle.High >= slPrice)
                return (TradeCloseReason.StopLoss, slPrice);
        }

        if (trade.TakeProfit.HasValue)
        {
            var tpPrice = isBuy ? entry + trade.TakeProfit.Value : entry - trade.TakeProfit.Value;
            if (isBuy ? candle.High >= tpPrice : candle.Low <= tpPrice)
                return (TradeCloseReason.TakeProfit, tpPrice);
        }

        return (null, null);
    }

    private static void CloseTrade(
        Trade trade,
        decimal closePrice,
        TradeCloseReason reason,
        DateTimeOffset time,
        ref decimal balance)
    {
        trade.StatusId      = (int)TradeStatus.Closed;
        trade.ClosedAt      = time;
        trade.ClosedPrice   = closePrice;
        trade.CloseReasonId = (int)reason;
        trade.Pnl           = trade.SideId == (int)TradeSide.Buy
            ? (closePrice - trade.EntryPrice!.Value) * trade.Quantity
            : (trade.EntryPrice!.Value - closePrice) * trade.Quantity;

        balance += trade.Pnl.Value;
    }

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
