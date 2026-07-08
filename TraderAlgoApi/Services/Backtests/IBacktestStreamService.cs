namespace TraderAlgoApi.Services.Backtests;

public interface IBacktestStreamService
{
    /// <summary>
    /// Accepts the WebSocket, ensures the backtest is computed (via the single-flight
    /// <see cref="BacktestJobRunner"/>), then replays the finished run to the client. A client
    /// disconnect stops the replay but never the underlying computation.
    /// </summary>
    Task StreamAsync(HttpContext context, long backtestId, bool delay = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs (or resumes) the simulation to a terminal state, persisting trades and progress. Owns
    /// its own <c>DbContext</c> and is safe to run detached from any request — invoked by
    /// <see cref="BacktestJobRunner"/> under the application lifetime.
    /// </summary>
    Task ComputeAsync(long backtestId, CancellationToken cancellationToken = default);
}
