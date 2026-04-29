namespace TraderAlgoApi.Services.Rules.Macd;

public sealed class MacdTradingRule : ITradingRule
{
    public bool IsMacdLineAboveSignalLine(TradingRuleContext context) =>
        context.CurrentMacdLine.HasValue && context.CurrentSignalLine.HasValue &&
        context.CurrentMacdLine.Value > context.CurrentSignalLine.Value;

    public bool IsMacdLineBelowSignalLine(TradingRuleContext context) =>
        context.CurrentMacdLine.HasValue && context.CurrentSignalLine.HasValue &&
        context.CurrentMacdLine.Value < context.CurrentSignalLine.Value;

    public bool IsHistogramAboveZero(TradingRuleContext context) =>
        context.CurrentHistogram.HasValue && context.CurrentHistogram.Value > 0m;

    public bool IsHistogramBelowZero(TradingRuleContext context) =>
        context.CurrentHistogram.HasValue && context.CurrentHistogram.Value < 0m;

    public bool IsHistogramIncreasing(TradingRuleContext context) =>
        context.CurrentHistogram.HasValue && context.PreviousHistogram.HasValue &&
        context.CurrentHistogram.Value > context.PreviousHistogram.Value;

    public bool IsHistogramDecreasing(TradingRuleContext context) =>
        context.CurrentHistogram.HasValue && context.PreviousHistogram.HasValue &&
        context.CurrentHistogram.Value < context.PreviousHistogram.Value;

    // Bearish momentum is weakening: histogram still below zero but shrinking upward.
    public bool ShouldEnterLong(TradingRuleContext context) =>
        IsMacdLineBelowSignalLine(context) &&
        IsHistogramBelowZero(context) &&
        IsHistogramIncreasing(context);

    // Bullish momentum is weakening: histogram still above zero but shrinking downward.
    public bool ShouldEnterShort(TradingRuleContext context) =>
        IsMacdLineAboveSignalLine(context) &&
        IsHistogramAboveZero(context) &&
        IsHistogramDecreasing(context);
}
