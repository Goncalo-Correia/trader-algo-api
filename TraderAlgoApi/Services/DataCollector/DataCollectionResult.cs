namespace TraderAlgoApi.Services.DataCollector;

public sealed record DataCollectionResult(
    string Symbol,
    string Interval,
    DateTimeOffset StartTime,
    DateTimeOffset EndTimeExclusive,
    DateTimeOffset? LatestCandleOpenTime,
    int FetchedCount,
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<DataCollectionError> Errors)
{
    /// <summary>Convenience flag for callers that only need to know whether anything failed.</summary>
    public bool HasErrors => Errors.Count > 0;
}
