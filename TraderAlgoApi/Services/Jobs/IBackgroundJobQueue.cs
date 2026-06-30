namespace TraderAlgoApi.Services.Jobs;

/// <summary>
/// In-process queue of sync-job ids awaiting execution. A single <see cref="SyncJobWorker"/>
/// drains it, so jobs run one at a time and never overlap.
/// </summary>
public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(long jobId, CancellationToken cancellationToken = default);

    ValueTask<long> DequeueAsync(CancellationToken cancellationToken);
}
