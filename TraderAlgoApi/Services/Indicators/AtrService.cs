namespace TraderAlgoApi.Services.Indicators;

public sealed class AtrService : IAtrService
{
    public (decimal? TrueRange, decimal? Atr)[] ComputeAll(
        IReadOnlyList<decimal> highs,
        IReadOnlyList<decimal> lows,
        IReadOnlyList<decimal> closes,
        int period = 14)
    {
        var count = closes.Count;
        var result = new (decimal? TrueRange, decimal? Atr)[count];

        if (count == 0)
            return result;

        // True range is defined for every candle. The first candle has no prior close,
        // so its true range is simply the high-low span.
        var trueRanges = new decimal[count];
        trueRanges[0] = highs[0] - lows[0];
        for (var i = 1; i < count; i++)
        {
            var highLow   = highs[i] - lows[i];
            var highClose = Math.Abs(highs[i] - closes[i - 1]);
            var lowClose  = Math.Abs(lows[i] - closes[i - 1]);
            trueRanges[i] = Math.Max(highLow, Math.Max(highClose, lowClose));
        }

        for (var i = 0; i < count; i++)
            result[i].TrueRange = trueRanges[i];

        if (count < period)
            return result;

        // Seed the ATR with the simple average of the first `period` true ranges.
        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += trueRanges[i];

        var atr = sum / period;
        result[period - 1].Atr = atr;

        // Wilder's smoothing for all subsequent candles.
        for (var i = period; i < count; i++)
        {
            atr = (atr * (period - 1) + trueRanges[i]) / period;
            result[i].Atr = atr;
        }

        return result;
    }
}
