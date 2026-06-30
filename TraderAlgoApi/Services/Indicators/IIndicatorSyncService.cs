using TraderAlgoApi.Models;

namespace TraderAlgoApi.Services.Indicators;

public interface IIndicatorSyncService
{
    /// <summary>
    /// Recomputes indicators across the full stored history of one symbol/interval. Used by the
    /// indicator full-sync job (e.g. after the indicator math changes).
    /// </summary>
    Task<IndicatorSyncResult> FullSyncPairAsync(
        Symbol symbol,
        Interval interval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes indicators only for candles of one symbol/interval that are still missing any
    /// indicator row, from the earliest gap forward. Used by the indicator partial-sync job.
    /// </summary>
    Task<IndicatorSyncResult> PartialSyncPairAsync(
        Symbol symbol,
        Interval interval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes and upserts indicator rows for every candle in [<paramref name="from"/>, <paramref name="to"/>]
    /// for the given symbol/interval. Loads up to 99 candles before <paramref name="from"/> for SMA context.
    /// </summary>
    Task ComputeAndSaveAsync(
        int symbolId,
        int intervalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
