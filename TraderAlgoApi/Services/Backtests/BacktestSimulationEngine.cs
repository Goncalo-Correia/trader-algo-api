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

    // ── ML bracket execution ───────────────────────────────────────────────────────
    // The MlPolicy strategy sizes its stop/take-profit per trade from the sidecar's bracket
    // decision and an ATR-at-entry, and runs an ATR-scaled breakeven ratchet. These helpers
    // mirror trader-algo-ml's TradingEnv (app/env/trading_env.py) exactly — stop fills before
    // TP on a spanning candle, entry/exit fills at price ± slippage, one flat fee per round-trip
    // — and are kept separate from the indicator-strategy path (shared CheckBreakeven above is
    // untouched). Stop/take-profit are stored on the Trade as positive unit offsets from the fill
    // price, so CheckSlTp/CloseTrade are reused for the trigger geometry; only the fill prices and
    // the breakeven trigger differ.

    /// <summary>
    /// ATR (Wilder, period 14) used to size the ML brackets. Mirrors the env's guard: a missing or
    /// non-positive ATR (indicator warmup) falls back to 1 so a bracket can always be sized.
    /// </summary>
    public static decimal AtrAtEntryOrFallback(decimal? atrValue) =>
        atrValue is decimal a && a > 0m ? a : 1m;

    /// <summary>Stop distance = slAtrMult × ATR-at-entry; take-profit distance = tpRMult × stop distance.</summary>
    public static (decimal StopDistance, decimal TakeProfitDistance) MlBracketDistances(
        decimal atrAtEntry, decimal slAtrMult, decimal tpRMult)
    {
        var stopDistance = slAtrMult * atrAtEntry;
        return (stopDistance, tpRMult * stopDistance);
    }

    /// <summary>Entry fills at candle close ± slippage (long +, short −), matching the env's _open_position.</summary>
    public static decimal MlEntryFillPrice(decimal closePrice, TradeSide side, decimal slippage) =>
        side == TradeSide.Buy ? closePrice + slippage : closePrice - slippage;

    /// <summary>
    /// ATR-scaled breakeven ratchet for an open ML trade, mirroring the env's
    /// _arm_breakeven_if_triggered. Trigger at entry ± breakeven × ATR-at-entry; once the candle's
    /// extreme reaches it, ratchet the stop toward entry ± breakevenStop × ATR-at-entry, never
    /// loosening it. Mutates <paramref name="trade"/>.StopLoss (a distance from entry; negative means
    /// the stop sits beyond entry, locking in profit). No-op when breakeven is disabled (≤ 0).
    /// Idempotent, so it is safe to call every candle without tracking an armed flag.
    /// </summary>
    public static bool TryArmMlBreakeven(Trade trade, decimal candleHigh, decimal candleLow, decimal breakeven, decimal breakevenStop)
    {
        if (breakeven <= 0m || trade.StopLoss is null || trade.EntryPrice is null)
            return false;

        var entry = trade.EntryPrice.Value;
        var isBuy = trade.SideId == (int)TradeSide.Buy;
        var atr   = AtrAtEntryOrFallback(trade.AtrAtEntry);

        var triggerPrice = isBuy ? entry + breakeven * atr : entry - breakeven * atr;
        var triggered = isBuy ? candleHigh >= triggerPrice : candleLow <= triggerPrice;
        if (!triggered)
            return false;

        var newStopPrice     = isBuy ? entry + breakevenStop * atr : entry - breakevenStop * atr;
        var currentStopPrice = isBuy ? entry - trade.StopLoss.Value : entry + trade.StopLoss.Value;
        var ratchetedStop    = isBuy
            ? Math.Max(currentStopPrice, newStopPrice)   // long: stop only moves up
            : Math.Min(currentStopPrice, newStopPrice);  // short: stop only moves down

        // Store back as a distance from entry (slPrice = entry ∓ distance).
        trade.StopLoss = isBuy ? entry - ratchetedStop : ratchetedStop - entry;
        return true;
    }

    /// <summary>
    /// Closes an ML trade at a bracket trigger (or forced) price, applying exit slippage
    /// (long −, short +) to the fill before the flat fee, matching the env's _close_position.
    /// </summary>
    public static decimal CloseMlTrade(
        Trade trade,
        decimal triggerPrice,
        TradeCloseReason reason,
        decimal slippage,
        DateTimeOffset time,
        ref decimal balance)
    {
        var exitPrice = trade.SideId == (int)TradeSide.Buy
            ? triggerPrice - slippage
            : triggerPrice + slippage;
        return CloseTrade(trade, exitPrice, reason, time, ref balance);
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
