using TraderAlgoApi.Services.Binance;

namespace TraderAlgoApi.Services.Charts;

public sealed class LiveChartDataService(
    IBinanceMarketDataWebSocketService binanceMarketDataWebSocketService) : ILiveChartDataService
{
    private const string DefaultSymbol = "BTC/USD";
    private const string DefaultInterval = "1h";

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
        var streamInterval = TryNormalizeInterval(interval);

        if (streamInterval is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Interval must be either '5m' or '1h'.", cancellationToken);
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
        await binanceMarketDataWebSocketService.StreamKlineCandlesAsync(
            clientSocket,
            streamSymbol,
            streamInterval,
            cancellationToken);
    }

    private static string? TryNormalizeInterval(string? interval)
    {
        var normalizedInterval = string.IsNullOrWhiteSpace(interval)
            ? DefaultInterval
            : interval.Trim().ToLowerInvariant();

        return normalizedInterval switch
        {
            "5m" or "5minute" or "5minutes" or "5min" or "5mins" => "5m",
            "1h" or "1hour" or "1hours" or "1hr" or "1hrs" => "1h",
            _ => null
        };
    }
}
