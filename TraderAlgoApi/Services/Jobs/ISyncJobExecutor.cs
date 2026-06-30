namespace TraderAlgoApi.Services.Jobs;

public interface ISyncJobExecutor
{
    /// <summary>Runs the job to completion, updating its durable status row throughout.</summary>
    Task ExecuteAsync(long jobId, CancellationToken cancellationToken);
}
