namespace TraderAlgoApi.Services.Rules.Sma;

public sealed class SmaTradingRule : ITradingRule
{
    public bool IsSma20AboveSma100(TradingRuleContext context) =>
        context.CurrentSma20.HasValue && context.CurrentSma100.HasValue &&
        context.CurrentSma20.Value > context.CurrentSma100.Value;

    public bool IsSma20BelowSma100(TradingRuleContext context) =>
        context.CurrentSma20.HasValue && context.CurrentSma100.HasValue &&
        context.CurrentSma20.Value < context.CurrentSma100.Value;

    // A retest is confirmed when the candle's wick touches SMA20 and the close confirms direction.
    // Bullish retest: uptrend (SMA20 > SMA100), candle touches SMA20, closes above it.
    // Bearish retest: downtrend (SMA20 < SMA100), candle touches SMA20, closes below it.
    public bool IsPriceRetestingSma20(TradingRuleContext context)
    {
        if (context.CurrentSma20 is not { } sma20)
            return false;

        var touchesSma20 = context.CurrentLow <= sma20 && context.CurrentHigh >= sma20;

        if (!touchesSma20)
            return false;

        if (IsSma20AboveSma100(context))
            return context.CurrentClose > sma20;

        if (IsSma20BelowSma100(context))
            return context.CurrentClose < sma20;

        return false;
    }

    public bool ShouldEnterLong(TradingRuleContext context) =>
        IsSma20AboveSma100(context) && IsPriceRetestingSma20(context);

    public bool ShouldEnterShort(TradingRuleContext context) =>
        IsSma20BelowSma100(context) && IsPriceRetestingSma20(context);
}
