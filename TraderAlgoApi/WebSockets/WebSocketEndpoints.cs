using TraderAlgoApi.Services.Charts;

namespace TraderAlgoApi.WebSockets;

internal static class WebSocketEndpoints
{
    internal static void MapWebSocketEndpoints(this WebApplication app)
    {
        app.MapGet("/ws/charts/candles", async (
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
