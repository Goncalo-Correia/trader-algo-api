namespace TraderAlgoApi.Services.Indicators;

public sealed record IndicatorSyncResult(
    string Symbol,
    string Interval,
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount);
