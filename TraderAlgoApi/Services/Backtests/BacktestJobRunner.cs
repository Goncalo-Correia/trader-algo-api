using System.Collections.Concurrent;

namespace TraderAlgoApi.Services.Backtests;

/// <summary>
/// Owns backtest computation as a background job, decoupled from any WebSocket client. A run is
/// started at most once per backtest id (single-flight): concurrent stream clients all await the
/// same shared <see cref="Task"/> instead of each kicking off their own simulation, which would
/// otherwise duplicate trades and corrupt the persisted result. The job runs under the application
/// lifetime — a client disconnect cancels only that client's <em>wait</em>, never the run itself —
/// so a browser refresh or dropped connection no longer cancels a long backtest.
/// </summary>
public sealed class BacktestJobRunner(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<BacktestJobRunner> logger)
{
    // Lazy<Task> so the compute factory runs exactly once even under concurrent GetOrAdd contention.
    private readonly ConcurrentDictionary<long, Lazy<Task>> _jobs = new();

    /// <summary>
    /// Ensures a compute job is running for the backtest and returns the shared task. Callers await
    /// it to know when the run has reached a terminal state; the task never faults (failures are
    /// logged and persisted as the backtest's status), so awaiting it is always safe.
    /// </summary>
    public Task EnsureRunAsync(long backtestId) =>
        _jobs.GetOrAdd(backtestId, id => new Lazy<Task>(() => RunAsync(id))).Value;

    private async Task RunAsync(long backtestId)
    {
        // Detach from the caller's execution context before doing work so the shared task is the
        // one registered in the dictionary rather than the synchronous prefix of the caller.
        await Task.Yield();

        try
        {
            // Fresh DI scope + DbContext, independent of any request/socket scope.
            await using var scope = scopeFactory.CreateAsyncScope();
            var stream = scope.ServiceProvider.GetRequiredService<IBacktestStreamService>();
            await stream.ComputeAsync(backtestId, lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            // Host shutting down mid-run: ComputeAsync leaves the backtest Running so a later
            // stream/start resumes it from persisted progress.
            logger.LogInformation("Backtest {Id} compute interrupted by host shutdown.", backtestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backtest {Id} background compute failed", backtestId);
        }
        finally
        {
            _jobs.TryRemove(backtestId, out _);
        }
    }
}
