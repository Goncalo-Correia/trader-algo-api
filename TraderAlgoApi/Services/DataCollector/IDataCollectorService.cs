namespace TraderAlgoApi.Services.DataCollector;

public interface IDataCollectorService
{
    Task<DataCollectionResult> CollectKlinesAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset startTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Backfills missing candles within <paramref name="windowStart"/>..now: any internal gaps,
    /// a leading gap before the first stored candle in range, and the trailing gap up to now.
    /// Pass <see cref="DataCollectorDefaults.DataStartDate"/> for a full-history sync, or a recent
    /// floor (e.g. now minus <see cref="DataCollectorDefaults.TimerLookback"/>) for cheap routine syncs.
    /// </summary>
    Task<DataCollectionResult> SyncGapsAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset windowStart,
        CancellationToken cancellationToken = default);
}
