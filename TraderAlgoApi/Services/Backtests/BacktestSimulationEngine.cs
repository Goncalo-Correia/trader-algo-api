using TraderAlgoApi.Dtos.Backtests;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Rules;

namespace TraderAlgoApi.Services.Backtests;

/// <summary>
/// Pure, I/O-free simulation math shared by the backtest stream (live run) and the
/// backtest service (after-the-fact equity/drawdown reporting). Nothing in here touches
/// the database, the WebSocket, or the clock — every method is deterministic from its
/// inputs, which keeps the P&amp;L-critical logic in one cohesive, testable place.
/// </summary>
public static class BacktestSimulationEngine
{
    public static readonly TimeZoneInfo EasternZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    public static readonly TimeOnly NyOpen  = new(9, 30);
    public static readonly TimeOnly NyClose = new(16, 0);

    // ── Session helpers ─────────────────────────────────────────────────────────

    public static bool IsNySessionCandle(KlineData k) =>
        IsWithinNySession(k.OpenTime);

    public static bool IsWithinNySession(DateTimeOffset time)
    {
        var eastern = TimeZoneInfo.ConvertTime(time, EasternZone);
        if (eastern.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;
        var tod = TimeOnly.FromDateTime(eastern.DateTime);
        return tod >= NyOpen && tod < NyClose;
    }

    public static DateOnly EasternDay(DateTimeOffset time) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, EasternZone).DateTime);

    public static bool CanEnterToday(
        TradeBot bot,
        DateTimeOffset candleTime,
        IReadOnlyDictionary<DateOnly, (decimal Pnl, int Losses)> dailyStats)
    {
        if (bot.IsNySessionOnly && !IsWithinNySession(candleTime))
            return false;

        if (bot.DailyProfitGoal.HasValue || bot.MaxLossesPerDay.HasValue)
        {
            var day = EasternDay(candleTime);
            dailyStats.TryGetValue(day, out var stats);

            if (bot.DailyProfitGoal.HasValue && stats.Pnl >= bot.DailyProfitGoal.Value)
                return false;

            if (bot.MaxLossesPerDay.HasValue && stats.Losses >= bot.MaxLossesPerDay.Value)
                return false;
        }

        return true;
    }

    // ── Trade lifecycle math ──────────────────────────────────────────────────────

    /// <summary>Returns the close reason and price when SL/TP is hit intra-candle, else (null, null).</summary>
    public static (TradeCloseReason? Reason, decimal? Price) CheckSlTp(Trade trade, KlineData candle)
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

    public static bool CheckBreakeven(Trade trade, KlineData candle, decimal breakevenThreshold)
    {
        var entry  = trade.EntryPrice!.Value;
        var isBuy  = trade.SideId == (int)TradeSide.Buy;
        var peakPnl = isBuy
            ? (candle.High - entry) * trade.Quantity
            : (entry - candle.Low)  * trade.Quantity;

        return peakPnl >= breakevenThreshold;
    }

    /// <summary>
    /// Mutates <paramref name="trade"/> into a closed state and advances <paramref name="balance"/>.
    /// Returns the realized (fee-adjusted) PnL for convenience.
    /// </summary>
    public static decimal CloseTrade(
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

        var rawPnl = trade.SideId == (int)TradeSide.Buy
            ? (closePrice - trade.EntryPrice!.Value) * trade.Quantity
            : (trade.EntryPrice!.Value - closePrice) * trade.Quantity;

        trade.Pnl        = rawPnl - trade.Fee;
        balance         += trade.Pnl.Value;
        trade.AccountPnl = balance;
        return trade.Pnl.Value;
    }

    // ── Equity / drawdown reporting ────────────────────────────────────────────────

    /// <summary>
    /// Builds the equity curve from the closed trades' PnL, ordered by close time.
    /// <paramref name="closedTrades"/> only needs ClosedAt + Pnl — callers can project to
    /// a lightweight shape instead of loading full trade graphs.
    /// </summary>
    public static IReadOnlyList<EquityPointDto> BuildEquityCurve(
        decimal initialBalance,
        long fromUnixSeconds,
        IEnumerable<(DateTimeOffset? ClosedAt, decimal? Pnl)> closedTrades)
    {
        var points = new List<EquityPointDto>
        {
            new(fromUnixSeconds, initialBalance, null)
        };

        var balance = initialBalance;

        foreach (var trade in closedTrades
                     .Where(t => t is { Pnl: not null, ClosedAt: not null })
                     .OrderBy(t => t.ClosedAt))
        {
            balance += trade.Pnl!.Value;
            points.Add(new EquityPointDto(
                trade.ClosedAt!.Value.ToUnixTimeSeconds(),
                balance,
                trade.Pnl));
        }

        return points;
    }

    /// <summary>
    /// Returns (maxDrawdown, maxTrailingDrawdown) from the equity curve.
    /// maxDrawdown         — largest absolute dollar amount the balance dropped below the initial balance.
    /// maxTrailingDrawdown — largest absolute dollar drop from any peak to any subsequent balance.
    /// Both are null when there are no closed trades.
    /// </summary>
    public static (decimal? MaxDrawdown, decimal? MaxTrailingDrawdown) ComputeDrawdowns(
        IReadOnlyList<EquityPointDto> equity,
        decimal initialBalance)
    {
        if (equity.Count <= 1)
            return (null, null);

        var peak   = initialBalance;
        var maxDD  = 0m;
        var maxTDD = 0m;

        foreach (var point in equity)
        {
            var belowStart = initialBalance - point.Balance;
            if (belowStart > maxDD)
                maxDD = belowStart;

            if (point.Balance > peak)
                peak = point.Balance;

            var dropFromPeak = peak - point.Balance;
            if (dropFromPeak > maxTDD)
                maxTDD = dropFromPeak;
        }

        return (maxDD > 0 ? maxDD : null, maxTDD > 0 ? maxTDD : null);
    }

    // ── Strategy context ───────────────────────────────────────────────────────────

    public static TradingRuleContext BuildContext(
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
}
