namespace TraderAlgoApi.Services.Rules.Rsi;

public sealed class RsiTradingRule : ITradingRule
{
    public bool IsRsiBelow30(TradingRuleContext context) =>
        context.CurrentRsi.HasValue && context.CurrentRsi.Value < 30m;

    public bool IsRsiAbove70(TradingRuleContext context) =>
        context.CurrentRsi.HasValue && context.CurrentRsi.Value > 70m;

    public bool IsRsiAboveSmoothRsi(TradingRuleContext context) =>
        context.CurrentRsi.HasValue && context.CurrentRsiSmooth.HasValue &&
        context.CurrentRsi.Value > context.CurrentRsiSmooth.Value;

    public bool IsRsiBelowSmoothRsi(TradingRuleContext context) =>
        context.CurrentRsi.HasValue && context.CurrentRsiSmooth.HasValue &&
        context.CurrentRsi.Value < context.CurrentRsiSmooth.Value;

    public bool ShouldEnterLong(TradingRuleContext context) =>
        IsRsiBelow30(context) && IsRsiAboveSmoothRsi(context);

    public bool ShouldEnterShort(TradingRuleContext context) =>
        IsRsiAbove70(context) && IsRsiBelowSmoothRsi(context);
}
