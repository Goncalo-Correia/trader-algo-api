using System.Net.WebSockets;

namespace TraderAlgoApi.Services.Binance;

public interface IBinanceMarketDataWebSocketService
{
    Task StreamKlinesAsync(
        WebSocket clientSocket,
        string symbol,
        string interval,
        CancellationToken cancellationToken = default);

    Task StreamKlineCandlesAsync(
        WebSocket clientSocket,
        string symbol,
        string interval,
        CancellationToken cancellationToken = default);
}
