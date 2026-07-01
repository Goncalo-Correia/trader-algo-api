using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.DataCollector;
using TraderAlgoApi.Services.Indicators;

namespace TraderAlgoApi.Services.Jobs;

public sealed class SyncJobExecutor(
    IServiceScopeFactory scopeFactory,
    ILogger<SyncJobExecutor> logger) : ISyncJobExecutor
{
    public async Task ExecuteAsync(long jobId, CancellationToken cancellationToken)
    {
        // A dedicated scope owns the job row for the whole run; per-pair work uses its own
        // short-lived scopes so the change tracker never accumulates across pairs.
        using var statusScope = scopeFactory.CreateScope();
        var statusDb = statusScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var job = await statusDb.SyncJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Sync job {JobId} not found; skipping", jobId);
            return;
        }

        logger.LogInformation("Starting sync job {JobId} ({Type})", job.Id, job.TypeEnum);

        job.StatusEnum     = SyncJobStatus.Running;
        job.StartedAt      = DateTimeOffset.UtcNow;
        job.CompletedUnits = 0;
        job.Error          = null;
        await statusDb.SaveChangesAsync(cancellationToken);

        try
        {
            var (symbols, intervals) = await LoadPairsAsync(statusDb, job.TypeEnum, cancellationToken);

            job.TotalUnits = symbols.Count * intervals.Count;
            job.Message    = $"0/{job.TotalUnits}";
            await statusDb.SaveChangesAsync(cancellationToken);

            var errorCount = 0;

            foreach (var symbol in symbols)
            {
                foreach (var interval in intervals)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var pairErrors = await RunPairAsync(job.TypeEnum, symbol, interval, cancellationToken);
                        foreach (var error in pairErrors)
                        {
                            errorCount++;
                            statusDb.SyncJobErrors.Add(new SyncJobError
                            {
                                SyncJobId      = job.Id,
                                Symbol         = error.Symbol,
                                Interval       = error.Interval,
                                CandleOpenTime = error.CandleOpenTime,
                                Message        = error.Message,
                            });
                            logger.LogWarning(
                                "Sync job {JobId}: {Symbol}/{Interval} candle {CandleOpenTime}: {Message}",
                                job.Id, error.Symbol, error.Interval, error.CandleOpenTime, error.Message);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errorCount++;
                        statusDb.SyncJobErrors.Add(new SyncJobError
                        {
                            SyncJobId      = job.Id,
                            Symbol         = symbol.Code,
                            Interval       = interval.Code,
                            CandleOpenTime = null, // whole-pair failure, not tied to a single candle
                            Message        = Truncate(ex.Message, 2000),
                        });
                        logger.LogError(ex,
                            "Sync job {JobId}: pair {Symbol}/{Interval} failed",
                            job.Id, symbol.Code, interval.Code);
                    }

                    job.CompletedUnits++;
                    job.Message = $"{symbol.Code}/{interval.Code} ({job.CompletedUnits}/{job.TotalUnits})"
                                + (errorCount > 0 ? $" — {errorCount} error(s)" : string.Empty);
                    await statusDb.SaveChangesAsync(cancellationToken);
                }
            }

            job.StatusEnum   = SyncJobStatus.Completed;
            job.CompletedAt  = DateTimeOffset.UtcNow;
            job.Message      = errorCount == 0
                ? $"Completed {job.TotalUnits} pair(s)"
                : $"Completed {job.TotalUnits} pair(s) with {errorCount} error(s)";
            await statusDb.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Sync job {JobId} completed: {Message}", job.Id, job.Message);
        }
        catch (OperationCanceledException)
        {
            // Host is stopping (or the job was cancelled). Record it without the now-cancelled token.
            job.StatusEnum  = SyncJobStatus.Cancelled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Message     = $"Cancelled at {job.CompletedUnits}/{job.TotalUnits}";
            await statusDb.SaveChangesAsync(CancellationToken.None);

            logger.LogWarning("Sync job {JobId} cancelled at {Done}/{Total}",
                job.Id, job.CompletedUnits, job.TotalUnits);
            throw;
        }
        catch (Exception ex)
        {
            job.StatusEnum  = SyncJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Error       = ex.Message;
            await statusDb.SaveChangesAsync(CancellationToken.None);

            logger.LogError(ex, "Sync job {JobId} failed", job.Id);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private async Task<(List<Symbol> Symbols, List<Interval> Intervals)> LoadPairsAsync(
        ApplicationDbContext db,
        Models.Enums.SyncJobType type,
        CancellationToken cancellationToken)
    {
        var symbolsQuery = db.Symbols.AsNoTracking().Where(s => s.IsActive);

        // Data collection only targets providers we ingest from; indicator recompute covers every
        // active symbol regardless of provider.
        if (type is Models.Enums.SyncJobType.DataCollectorFullSync or Models.Enums.SyncJobType.DataCollectorPartialSync)
            symbolsQuery = symbolsQuery.Where(s => s.ProviderId == (int)SymbolProvider.Binance);

        var symbols = await symbolsQuery.OrderBy(s => s.Code).ToListAsync(cancellationToken);

        var intervals = await db.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Duration)
            .ToListAsync(cancellationToken);

        return (symbols, intervals);
    }

    private async Task<IReadOnlyList<DataCollectionError>> RunPairAsync(
        Models.Enums.SyncJobType type,
        Symbol symbol,
        Interval interval,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        switch (type)
        {
            case Models.Enums.SyncJobType.DataCollectorFullSync:
                return (await services.GetRequiredService<IBinanceDataCollectorService>()
                    .CollectKlinesAsync(symbol.Code, interval.Code, DataCollectorDefaults.DataStartDate, cancellationToken))
                    .Errors;

            case Models.Enums.SyncJobType.DataCollectorPartialSync:
                return (await services.GetRequiredService<IBinanceDataCollectorService>()
                    .SyncGapsAsync(symbol.Code, interval.Code, DataCollectorDefaults.DataStartDate, cancellationToken))
                    .Errors;

            case Models.Enums.SyncJobType.IndicatorFullSync:
                await services.GetRequiredService<IIndicatorSyncService>()
                    .FullSyncPairAsync(symbol, interval, cancellationToken);
                return [];

            case Models.Enums.SyncJobType.IndicatorPartialSync:
                await services.GetRequiredService<IIndicatorSyncService>()
                    .PartialSyncPairAsync(symbol, interval, cancellationToken);
                return [];

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown sync job type");
        }
    }
}
