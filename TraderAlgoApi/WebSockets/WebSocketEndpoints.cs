using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.Charts;

namespace TraderAlgoApi.WebSockets;

internal static class WebSocketEndpoints
{
    internal static void MapWebSocketEndpoints(this WebApplication app)
    {
        app.Map("/ws/binance/klines", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected a WebSocket request.");
                return;
            }

            var symbol = context.Request.Query["symbol"].FirstOrDefault() ?? "BTCUSDT";
            var interval = context.Request.Query["interval"].FirstOrDefault() ?? "1m";
            var streamService = context.RequestServices.GetRequiredService<IBinanceMarketDataService>();

            using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
            await streamService.StreamKlinesAsync(clientSocket, symbol, interval, context.RequestAborted);
        });

        app.MapGet("/ws/charts/{symbol}/candles", async (
            HttpContext context,
            string symbol,
            ILiveChartDataService liveChartDataService,
            CancellationToken cancellationToken) =>
        {
            var interval = context.Request.Query["interval"].FirstOrDefault();

            await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
        })
        .ExcludeFromDescription();
    }
}
