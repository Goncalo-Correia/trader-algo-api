using TraderAlgoApi.Services.Binance;

namespace TraderAlgoApi.Services.Charts;

public sealed class LiveChartDataService(
    IBinanceMarketDataWebSocketService binanceMarketDataWebSocketService,
    IChartsService chartsService) : ILiveChartDataService
{
    private const string DefaultSymbol = "BTC/USD";

    public async Task StreamCandlesAsync(
        HttpContext context,
        string? symbol = null,
        string? interval = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsync(
                "This endpoint requires a WebSocket connection. Use ws:// or wss:// instead of http:// or https://.",
                cancellationToken);
            return;
        }

        var streamSymbol = string.IsNullOrWhiteSpace(symbol) ? DefaultSymbol : symbol;
        var streamInterval = chartsService.NormalizeInterval(interval);

        if (!IsSupportedInterval(streamInterval))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Interval must be one of: 1m, 5m, 15m, 1h, 4h, 1d.", cancellationToken);
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
        await binanceMarketDataWebSocketService.StreamKlineCandlesAsync(
            clientSocket,
            streamSymbol,
            streamInterval,
            cancellationToken);
    }

    private static bool IsSupportedInterval(string interval) =>
        interval is "1m" or "5m" or "15m" or "1h" or "4h" or "1d";
}
