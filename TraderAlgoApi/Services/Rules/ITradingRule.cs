namespace TraderAlgoApi.Services.Rules;

public interface ITradingRule
{
    bool ShouldEnterLong(TradingRuleContext context);
    bool ShouldEnterShort(TradingRuleContext context);
}
