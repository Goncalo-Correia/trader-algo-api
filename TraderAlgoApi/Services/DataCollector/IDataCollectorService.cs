namespace TraderAlgoApi.Services.DataCollector;

public interface IDataCollectorService
{
    Task<DataCollectionResult> CollectKlinesAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset startTime,
        CancellationToken cancellationToken = default);

    Task<DataCollectionResult> SyncGapsAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset fallbackStartTime,
        CancellationToken cancellationToken = default);
}
