using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Binance;

namespace TraderAlgoApi.Services.Charts;

public sealed class LiveChartDataService(
    IBinanceMarketDataService binanceMarketDataService,
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
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : symbol;

        var streamInterval = string.IsNullOrWhiteSpace(interval)
            ? await dbContext.Intervals
                .Where(i => i.IsDefault)
                .Select(i => i.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : interval;

        var isValidInterval = await dbContext.Intervals
            .AnyAsync(i => i.IsActive && i.Code == streamInterval, cancellationToken);

        if (!isValidInterval)
        {
            var validCodes = await dbContext.Intervals
                .Where(i => i.IsActive)
                .OrderBy(i => i.Duration)
                .Select(i => i.Code)
                .ToListAsync(cancellationToken);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(
                $"Interval must be one of: {string.Join(", ", validCodes)}.",
                cancellationToken);
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
