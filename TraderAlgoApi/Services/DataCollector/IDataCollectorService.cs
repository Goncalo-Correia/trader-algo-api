namespace TraderAlgoApi.Services.DataCollector;

public interface IDataCollectorService
{
    Task<DataCollectionResult> CollectKlinesAsync(
        string symbolCode,
        string intervalCode,
        CancellationToken cancellationToken = default);
}
