namespace TraderAlgoApi.Services.DataCollector;

public static class DataCollectorDefaults
{
    /// <summary>
    /// The earliest open time from which historical kline data is collected.
    /// Providers are queried from this date forward, so full syncs backfill since 2020.
    /// </summary>
    public static readonly DateTimeOffset DataStartDate = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// How far back the nightly timer inspects for gaps. The API runs continuously and the
    /// full backfill is handled by the manual sync endpoints, so the timer only needs to cover
    /// realistic transient failures (a few missed nightly runs, late candle revisions). Seven
    /// days absorbs up to a week of downtime while keeping each nightly scan cheap.
    /// </summary>
    public static readonly TimeSpan TimerLookback = TimeSpan.FromDays(7);
}
