namespace TraderAlgoApi.Services.Indicators;

public interface IAtrService
{
    /// <summary>
    /// Computes the true range and Wilder's Average True Range for every candle in the series.
    /// Returns an array of the same length. TrueRange is populated at every index (the first
    /// candle uses high−low, having no prior close); Atr is null for indices before
    /// <paramref name="period"/> − 1 (insufficient history to seed the average).
    /// </summary>
    (decimal? TrueRange, decimal? Atr)[] ComputeAll(
        IReadOnlyList<decimal> highs,
        IReadOnlyList<decimal> lows,
        IReadOnlyList<decimal> closes,
        int period = 14);
}
