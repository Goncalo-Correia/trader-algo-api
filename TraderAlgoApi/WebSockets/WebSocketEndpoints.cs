using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.TradeEvents;

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

        app.MapGet("/ws/charts/candleswithindicators", async (
            HttpContext context,
            string symbol,
            string interval,
            ILiveChartDataService liveChartDataService,
            CancellationToken cancellationToken) =>
        {
            await liveChartDataService.StreamCandlesWithIndicatorsAsync(context, symbol, interval, cancellationToken);
        })
        .ExcludeFromDescription();

        app.MapGet("/ws/tradebots/events", async (
            HttpContext context,
            long? tradingAccountId,
            ITradeEventStreamService tradeEventStreamService,
            CancellationToken cancellationToken) =>
        {
            await tradeEventStreamService.StreamAsync(context, tradingAccountId, cancellationToken);
        })
        .ExcludeFromDescription();
    }
}
