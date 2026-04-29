namespace TraderAlgoApi.Services.Indicators;

public interface IMacdService
{
    /// <summary>
    /// Computes MACD values for every position in <paramref name="closes"/>.
    /// Returns an array of the same length.
    /// MacdLine is null for indices before <c>slowPeriod - 1</c>.
    /// SignalLine and Histogram are null for indices before <c>slowPeriod + signalPeriod - 2</c>.
    /// </summary>
    (decimal? MacdLine, decimal? SignalLine, decimal? Histogram)[] ComputeAll(
        IReadOnlyList<decimal> closes,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9);
}
