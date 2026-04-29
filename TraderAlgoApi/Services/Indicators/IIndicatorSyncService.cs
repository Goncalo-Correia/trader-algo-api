namespace TraderAlgoApi.Services.Indicators;

public interface IIndicatorSyncService
{
    Task<IReadOnlyList<IndicatorSyncResult>> FullSyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IndicatorSyncResult>> PartialSyncAsync(CancellationToken cancellationToken = default);

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
