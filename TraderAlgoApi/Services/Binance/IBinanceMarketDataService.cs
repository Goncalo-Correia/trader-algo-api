using System.Net.WebSockets;

namespace TraderAlgoApi.Services.Binance;

public interface IBinanceMarketDataService
{
    Task<IReadOnlyList<BinanceKline>> GetKlinesAsync(
        string symbol,
        string interval,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task StreamKlineCandlesAsync(
        WebSocket clientSocket,
        string symbol,
        string interval,
        CancellationToken cancellationToken = default);
}
