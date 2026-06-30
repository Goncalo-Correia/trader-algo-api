using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.Jobs;

/// <summary>
/// Drains the <see cref="IBackgroundJobQueue"/> one job at a time, on the application lifetime
/// token rather than any request token. On startup it re-queues jobs left non-terminal by a
/// previous process so an interrupted sync resumes (the syncs are idempotent/resumable).
/// </summary>
public sealed class SyncJobWorker(
    IBackgroundJobQueue queue,
    ISyncJobExecutor executor,
    IServiceScopeFactory scopeFactory,
    ILogger<SyncJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            long jobId;
            try
            {
                jobId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await executor.ExecuteAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync job {JobId} threw outside the executor", jobId);
            }
        }
    }

    private async Task RecoverInterruptedJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pendingId = (int)SyncJobStatus.Pending;
            var runningId = (int)SyncJobStatus.Running;

            var orphans = await db.SyncJobs
                .Where(j => j.StatusId == pendingId || j.StatusId == runningId)
                .OrderBy(j => j.Id)
                .ToListAsync(cancellationToken);

            if (orphans.Count == 0)
                return;

            foreach (var job in orphans)
            {
                job.StatusEnum     = SyncJobStatus.Pending;
                job.CompletedUnits = 0;
                job.Message        = "Re-queued after restart";
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (var job in orphans)
                await queue.EnqueueAsync(job.Id, cancellationToken);

            logger.LogInformation("Re-queued {Count} interrupted sync job(s) after restart", orphans.Count);
        }
        catch (Exception ex)
        {
            // Recovery is best-effort; never let it stop the worker from serving new jobs.
            logger.LogError(ex, "Failed to recover interrupted sync jobs on startup");
        }
    }
}
