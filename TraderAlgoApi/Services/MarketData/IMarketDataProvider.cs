namespace TraderAlgoApi.Services.MarketData;

/// <summary>
/// Provider-neutral interface for fetching historical candle data.
/// Implementations: BinanceMarketDataService.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>Maximum bars returned per REST request (used for pagination).</summary>
    int MaxPageSize { get; }

    Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string          symbol,
        string          intervalCode,
        DateTimeOffset? startTime         = null,
        DateTimeOffset? endTime           = null,
        int?            limit             = null,
        CancellationToken cancellationToken = default);
}
