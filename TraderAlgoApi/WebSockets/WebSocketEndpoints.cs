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
            var streamService = context.RequestServices.GetRequiredService<IBinanceMarketDataWebSocketService>();

            using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
            await streamService.StreamKlinesAsync(clientSocket, symbol, interval, context.RequestAborted);
        });

        app.MapGet("/ws/charts/candles", async (
            HttpContext context,
            ILiveChartDataService liveChartDataService,
            CancellationToken cancellationToken) =>
        {
            var symbol = context.Request.Query["symbol"].FirstOrDefault();
            var interval = context.Request.Query["interval"].FirstOrDefault();

            await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
        })
        .ExcludeFromDescription();

        app.MapGet("/ws/charts/candles/{interval}", async (
            HttpContext context,
            string interval,
            ILiveChartDataService liveChartDataService,
            CancellationToken cancellationToken) =>
        {
            var symbol = context.Request.Query["symbol"].FirstOrDefault();

            await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
        })
        .ExcludeFromDescription();

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

        app.MapGet("/ws/charts/{symbol}/candles/{interval}", async (
            HttpContext context,
            string symbol,
            string interval,
            ILiveChartDataService liveChartDataService,
            CancellationToken cancellationToken) =>
        {
            await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
        })
        .ExcludeFromDescription();
    }
}
