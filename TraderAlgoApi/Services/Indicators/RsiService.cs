namespace TraderAlgoApi.Services.Indicators;

public sealed class RsiService : IRsiService
{
    public decimal?[] ComputeAll(IReadOnlyList<decimal> closes, int period = 14)
    {
        var result = new decimal?[closes.Count];

        if (closes.Count <= period)
            return result;

        // Accumulate gains and losses for the seed average (indices 1..period)
        var avgGain = 0m;
        var avgLoss = 0m;
        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change > 0) avgGain += change;
            else avgLoss -= change;
        }
        avgGain /= period;
        avgLoss /= period;

        result[period] = Rsi(avgGain, avgLoss);

        // Wilder's smoothing for all subsequent candles
        for (var i = period + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0m;
            var loss = change < 0 ? -change : 0m;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            result[i] = Rsi(avgGain, avgLoss);
        }

        return result;
    }

    public decimal? ComputeSmooth(IReadOnlyList<decimal?> rsiValues, int index, int smoothPeriod = 3)
    {
        if (index < smoothPeriod - 1)
            return null;

        var sum = 0m;
        for (var i = index - smoothPeriod + 1; i <= index; i++)
        {
            if (rsiValues[i] is null)
                return null;
            sum += rsiValues[i]!.Value;
        }
        return sum / smoothPeriod;
    }

    public bool? DetectDivergence(IReadOnlyList<decimal> closes, IReadOnlyList<decimal?> rsiValues, int index, int lookback = 5)
    {
        if (index < lookback)
            return null;

        if (rsiValues[index] is null)
            return null;

        var currentRsi = rsiValues[index]!.Value;
        var currentClose = closes[index];

        // Find the swing low and swing high in the lookback window (excluding current candle)
        var windowStart = index - lookback;
        var minClose = closes[windowStart];
        var minIdx = windowStart;
        var maxClose = closes[windowStart];
        var maxIdx = windowStart;

        for (var i = windowStart + 1; i < index; i++)
        {
            if (closes[i] < minClose) { minClose = closes[i]; minIdx = i; }
            if (closes[i] > maxClose) { maxClose = closes[i]; maxIdx = i; }
        }

        // Bullish divergence: current close is a lower low, but RSI is a higher low
        var bullish = currentClose < minClose
            && rsiValues[minIdx].HasValue
            && currentRsi > rsiValues[minIdx]!.Value;

        // Bearish divergence: current close is a higher high, but RSI is a lower high
        var bearish = currentClose > maxClose
            && rsiValues[maxIdx].HasValue
            && currentRsi < rsiValues[maxIdx]!.Value;

        return bullish || bearish;
    }

    private static decimal Rsi(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0m) return 100m;
        if (avgGain == 0m) return 0m;
        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }
}
