namespace TraderAlgoApi.Services.Binance;

public interface IBinanceMarketDataService
{
    Task<IReadOnlyList<BinanceKline>> getKlines(
        string symbol,
        string interval,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}
