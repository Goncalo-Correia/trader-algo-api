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
    int SkippedCount);
