namespace TraderAlgoApi.Services.Indicators;

public interface ISimpleMovingAverageService
{
    /// <summary>
    /// Computes SMA20 and SMA100 for the candle at <paramref name="index"/> within <paramref name="closes"/>.
    /// Returns null for each SMA when insufficient history exists (fewer than 20 or 100 values).
    /// </summary>
    (decimal? Sma20, decimal? Sma100) Compute(IReadOnlyList<decimal> closes, int index);
}
