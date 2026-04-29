namespace TraderAlgoApi.Services.Indicators;

public interface IRsiService
{
    /// <summary>
    /// Computes RSI for every position in <paramref name="closes"/> using Wilder's smoothing method.
    /// Returns an array of the same length; entries before index <c>period</c> are null (insufficient history).
    /// </summary>
    decimal?[] ComputeAll(IReadOnlyList<decimal> closes, int period = 14);

    /// <summary>
    /// Returns the <paramref name="smoothPeriod"/>-candle SMA of the RSI values ending at <paramref name="index"/>.
    /// Returns null when fewer than <paramref name="smoothPeriod"/> consecutive non-null RSI values are available.
    /// </summary>
    decimal? ComputeSmooth(IReadOnlyList<decimal?> rsiValues, int index, int smoothPeriod = 3);

    /// <summary>
    /// Detects bullish or bearish RSI divergence at <paramref name="index"/> by comparing the current candle
    /// against the swing high/low found in the prior <paramref name="lookback"/> candles.
    /// Returns null when there is insufficient history.
    /// </summary>
    bool? DetectDivergence(IReadOnlyList<decimal> closes, IReadOnlyList<decimal?> rsiValues, int index, int lookback = 5);
}
