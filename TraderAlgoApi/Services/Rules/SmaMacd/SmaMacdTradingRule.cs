namespace TraderAlgoApi.Services.Rules.SmaMacd;

public sealed class SmaMacdTradingRule : ITradingRule
{
    public bool IsSma20AboveSma100(TradingRuleContext context) =>
        context.CurrentSma20.HasValue && context.CurrentSma100.HasValue &&
        context.CurrentSma20.Value > context.CurrentSma100.Value;

    public bool IsSma20BelowSma100(TradingRuleContext context) =>
        context.CurrentSma20.HasValue && context.CurrentSma100.HasValue &&
        context.CurrentSma20.Value < context.CurrentSma100.Value;

    public bool IsMacdLineAboveZero(TradingRuleContext context) =>
        context.CurrentMacdLine.HasValue && context.CurrentMacdLine.Value > 0m;

    public bool IsMacdLineBelowZero(TradingRuleContext context) =>
        context.CurrentMacdLine.HasValue && context.CurrentMacdLine.Value < 0m;

    public bool IsHistogramAboveZero(TradingRuleContext context) =>
        context.CurrentHistogram.HasValue && context.CurrentHistogram.Value > 0m;

    public bool IsHistogramBelowZero(TradingRuleContext context) =>
        context.CurrentHistogram.HasValue && context.CurrentHistogram.Value < 0m;

    public bool IsHistogramIncreasing(TradingRuleContext context) =>
        context.PreviousHistogram.HasValue && context.CurrentHistogram.HasValue &&
        context.PreviousHistogram.Value < context.CurrentHistogram.Value;

    public bool IsHistogramDecreasing(TradingRuleContext context) =>
        context.PreviousHistogram.HasValue && context.CurrentHistogram.HasValue &&
        context.PreviousHistogram.Value > context.CurrentHistogram.Value;

    // SMA confirms uptrend; MACD is bullish but histogram is still negative and rising.
    public bool ShouldEnterLong(TradingRuleContext context) =>
        IsSma20AboveSma100(context) &&
        IsMacdLineAboveZero(context) &&
        IsHistogramBelowZero(context) &&
        IsHistogramIncreasing(context);

    // SMA confirms downtrend; MACD is bearish but histogram is still positive and falling.
    public bool ShouldEnterShort(TradingRuleContext context) =>
        IsSma20BelowSma100(context) &&
        IsMacdLineBelowZero(context) &&
        IsHistogramAboveZero(context) &&
        IsHistogramDecreasing(context);
}
