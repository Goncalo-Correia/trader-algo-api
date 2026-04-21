using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Binance;

namespace TraderAlgoApi.Services.Charts;

public sealed class LiveChartDataService(
    IBinanceMarketDataService binanceMarketDataService,
    IChartsService chartsService,
    ApplicationDbContext dbContext) : ILiveChartDataService
{
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

        var streamSymbol = string.IsNullOrWhiteSpace(symbol)
            ? await dbContext.Symbols
                .Where(s => s.IsDefault)
                .Select(s => s.DisplayName)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : symbol;

        var activeIntervalCodes = await dbContext.Intervals
            .Where(i => i.IsActive)
            .Select(i => i.Code)
            .ToHashSetAsync(cancellationToken);

        var streamInterval = chartsService.NormalizeInterval(interval);

        if (!activeIntervalCodes.Contains(streamInterval))
        {
            var supported = string.Join(", ", activeIntervalCodes.Order());
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync($"Interval must be one of: {supported}.", cancellationToken);
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
        await binanceMarketDataService.StreamKlineCandlesAsync(
            clientSocket,
            streamSymbol,
            streamInterval,
            cancellationToken);
    }
}
