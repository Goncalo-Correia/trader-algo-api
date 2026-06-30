using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.Jobs;

public sealed class SyncJobService(
    ApplicationDbContext dbContext,
    IBackgroundJobQueue queue) : ISyncJobService
{
    public async Task<SyncJob> CreateAndQueueAsync(SyncJobType type, CancellationToken cancellationToken = default)
    {
        // Enum can't be used in the LINQ predicate, so compare the backing ints directly.
        var typeId      = (int)type;
        var pendingId   = (int)SyncJobStatus.Pending;
        var runningId   = (int)SyncJobStatus.Running;

        var existing = await dbContext.SyncJobs
            .Where(j => j.TypeId == typeId && (j.StatusId == pendingId || j.StatusId == runningId))
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return existing;

        var job = new SyncJob
        {
            TypeEnum   = type,
            StatusEnum = SyncJobStatus.Pending,
            CreatedAt  = DateTimeOffset.UtcNow,
        };

        dbContext.SyncJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(job.Id, cancellationToken);

        return job;
    }

    public async Task<SyncJob?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        await dbContext.SyncJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SyncJob>> ListRecentAsync(int take, CancellationToken cancellationToken = default) =>
        await dbContext.SyncJobs
            .AsNoTracking()
            .OrderByDescending(j => j.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}
