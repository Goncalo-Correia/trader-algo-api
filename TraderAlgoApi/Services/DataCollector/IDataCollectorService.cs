namespace TraderAlgoApi.Services.DataCollector;

public interface IDataCollectorService
{
    Task<DataCollectionResult> CollectKlinesAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset startTime,
        CancellationToken cancellationToken = default);
}
