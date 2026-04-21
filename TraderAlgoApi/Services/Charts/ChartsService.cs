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
        if (string.IsNullOrWhiteSpace(interval))
            return DefaultInterval;

        return interval.Trim().ToLowerInvariant() switch
        {
            "1minute" or "1minutes" or "1min" or "1mins" => "1m",
            "5minute" or "5minutes" or "5min" or "5mins" => "5m",
            "15minute" or "15minutes" or "15min" or "15mins" => "15m",
            "1hour" or "1hours" or "1hr" or "1hrs" => "1h",
            "4hour" or "4hours" or "4hr" or "4hrs" => "4h",
            "1day" or "1days" => "1d",
            var normalized => normalized
        };
    }
}
