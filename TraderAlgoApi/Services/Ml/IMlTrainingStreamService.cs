namespace TraderAlgoApi.Services.Ml;

public interface IMlTrainingStreamService
{
    /// <summary>
    /// Replays a trained model's decision log candle-by-candle over a WebSocket,
    /// emitting each candle and the model's aligned decision so the decision process
    /// can be visualized the same way an automated backtest is.
    /// </summary>
    Task StreamAsync(
        HttpContext context,
        long trainingRunId,
        bool delay = false,
        CancellationToken cancellationToken = default);
}
