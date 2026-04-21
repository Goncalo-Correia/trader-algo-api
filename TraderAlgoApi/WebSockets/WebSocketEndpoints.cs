using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
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

            var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();

            var symbol = context.Request.Query["symbol"].FirstOrDefault()
                ?? await dbContext.Symbols
                    .Where(s => s.IsDefault)
                    .Select(s => s.Code)
                    .FirstOrDefaultAsync(context.RequestAborted);

            var interval = context.Request.Query["interval"].FirstOrDefault()
                ?? await dbContext.Intervals
                    .Where(i => i.IsDefault)
                    .Select(i => i.Code)
                    .FirstOrDefaultAsync(context.RequestAborted);

            if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(interval))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Could not resolve symbol or interval.");
                return;
            }

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
