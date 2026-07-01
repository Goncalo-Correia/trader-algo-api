namespace TraderAlgoApi.Services.DataCollector;

/// <summary>
/// A candle that failed to collect, surfaced on <see cref="DataCollectionResult"/> so the caller can
/// inspect what failed and where. The job executor maps these to persisted
/// <see cref="Models.SyncJobError"/> rows; the timer/synchronous callers just log them.
/// </summary>
/// <param name="Symbol">Code of the symbol being collected when the error occurred.</param>
/// <param name="Interval">Code of the interval being collected when the error occurred.</param>
/// <param name="CandleOpenTime">
/// Open time of the candle the error refers to, or <see langword="null"/> when the failure could not
/// be tied to a single candle (e.g. a batch persist covering several candles).
/// </param>
/// <param name="Message">Human-readable description of the failure.</param>
public sealed record DataCollectionError(
    string Symbol,
    string Interval,
    DateTimeOffset? CandleOpenTime,
    string Message);
