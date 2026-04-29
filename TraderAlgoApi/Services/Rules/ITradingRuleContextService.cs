namespace TraderAlgoApi.Services.Rules;

public interface ITradingRuleContextService
{
    Task<TradingRuleContext?> GetLatestContextAsync(string symbolCode, string intervalCode, CancellationToken cancellationToken = default);
}
