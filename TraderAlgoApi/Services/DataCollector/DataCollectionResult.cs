namespace TraderAlgoApi.Services.DataCollector;

public sealed record DataCollectionResult(
    string Symbol,
    string Interval,
    DateTimeOffset StartTime,
    DateTimeOffset EndTimeExclusive,
    int FetchedCount,
    int InsertedCount,
    int SkippedCount);
