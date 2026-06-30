using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;

namespace TraderAlgoApi.Services.DataCollector;

public sealed class DataCollectorTimer(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DataCollectorTimer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextMidnightUtc();

            logger.LogInformation("Next data collection scheduled in {Delay}", delay);

            await Task.Delay(delay, timeProvider, stoppingToken);

            await CollectAllAsync(stoppingToken);
        }
    }

    private async Task CollectAllAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting scheduled data collection");

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext        = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var binanceService   = scope.ServiceProvider.GetRequiredService<IBinanceDataCollectorService>();

        var symbols = await dbContext.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        var intervals = await dbContext.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Duration)
            .ToListAsync(cancellationToken);

        var windowStart = timeProvider.GetUtcNow() - DataCollectorDefaults.TimerLookback;

        foreach (var symbol in symbols)
        {
            foreach (var interval in intervals)
            {
                try
                {
                    var result = await binanceService.SyncGapsAsync(
                        symbol.Code,
                        interval.Code,
                        windowStart,
                        cancellationToken);

                    logger.LogInformation(
                        "Synced {Symbol}/{Interval}: {Inserted} inserted, {Skipped} skipped",
                        result.Symbol, result.Interval, result.InsertedCount, result.SkippedCount);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to sync data for {Symbol}/{Interval}", symbol.Code, interval.Code);
                }
            }
        }

        logger.LogInformation("Scheduled data collection completed");
    }

    private TimeSpan GetDelayUntilNextMidnightUtc()
    {
        var now = timeProvider.GetUtcNow();
        var nextMidnight = now.Date.AddDays(1);
        return nextMidnight - now.UtcDateTime;
    }
}
