namespace TraderAlgoApi.Services.Charts;

public sealed class ChartsService : IChartsService
{
    private const string DefaultInterval = "1h";

    public string NormalizeSymbol(string symbol)
    {
        return symbol
            .Trim()
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    public string NormalizeInterval(string? interval)
    {
        return string.IsNullOrWhiteSpace(interval)
            ? DefaultInterval
            : interval.Trim().ToLowerInvariant();
    }
}
