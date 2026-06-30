using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.Jobs;

public interface ISyncJobService
{
    /// <summary>
    /// Creates a pending job of the given type and enqueues it. If an active (pending/running) job
    /// of the same type already exists, that one is returned instead so a sync can't be double-run.
    /// </summary>
    Task<SyncJob> CreateAndQueueAsync(SyncJobType type, CancellationToken cancellationToken = default);

    Task<SyncJob?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncJob>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
}
