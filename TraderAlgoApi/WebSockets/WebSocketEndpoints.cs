using TraderAlgoApi.Services.Charts;

namespace TraderAlgoApi.WebSockets;

internal static class WebSocketEndpoints
{
    internal static void MapWebSocketEndpoints(this WebApplication app)
    {
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
