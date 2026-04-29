namespace TraderAlgoApi.Services.Indicators;

public sealed class MacdService : IMacdService
{
    public (decimal? MacdLine, decimal? SignalLine, decimal? Histogram)[] ComputeAll(
        IReadOnlyList<decimal> closes,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        var count = closes.Count;
        var result = new (decimal? MacdLine, decimal? SignalLine, decimal? Histogram)[count];

        if (count < slowPeriod)
            return result;

        var fastEmas = ComputeEma(closes, fastPeriod);
        var slowEmas = ComputeEma(closes, slowPeriod);

        // MACD line is valid from the first index where the slow EMA is seeded (slowPeriod - 1).
        // fastEmas is guaranteed non-null at every index >= fastPeriod - 1 < slowPeriod - 1.
        var macdLines = new decimal?[count];
        for (var i = slowPeriod - 1; i < count; i++)
            macdLines[i] = fastEmas[i]!.Value - slowEmas[i]!.Value;

        // Signal line: EMA of the MACD values, seeded from the first available MACD value.
        var signalLines = ComputeEmaFromSequence(macdLines, signalPeriod, slowPeriod - 1);

        for (var i = 0; i < count; i++)
        {
            var macd = macdLines[i];
            var signal = signalLines[i];
            result[i] = (macd, signal, macd.HasValue && signal.HasValue ? macd.Value - signal.Value : null);
        }

        return result;
    }

    // Seeds from the SMA of the first `period` values, then applies standard EMA smoothing.
    private static decimal?[] ComputeEma(IReadOnlyList<decimal> closes, int period)
    {
        var result = new decimal?[closes.Count];
        if (closes.Count < period)
            return result;

        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += closes[i];

        var ema = sum / period;
        result[period - 1] = ema;

        var k = 2m / (period + 1);
        for (var i = period; i < closes.Count; i++)
        {
            ema = closes[i] * k + ema * (1m - k);
            result[i] = ema;
        }

        return result;
    }

    // Computes EMA over a nullable source array, seeding from index `startFrom`.
    private static decimal?[] ComputeEmaFromSequence(decimal?[] values, int period, int startFrom)
    {
        var result = new decimal?[values.Length];
        var seedEnd = startFrom + period - 1;

        if (seedEnd >= values.Length)
            return result;

        var sum = 0m;
        for (var i = startFrom; i <= seedEnd; i++)
        {
            if (!values[i].HasValue)
                return result;
            sum += values[i]!.Value;
        }

        var ema = sum / period;
        result[seedEnd] = ema;

        var k = 2m / (period + 1);
        for (var i = seedEnd + 1; i < values.Length; i++)
        {
            if (!values[i].HasValue)
                break;
            ema = values[i]!.Value * k + ema * (1m - k);
            result[i] = ema;
        }

        return result;
    }
}
