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
        // Pivot candidates span [index-lookback, index-1]; each pivot j needs closes[j-1] and closes[j+1],
        // so the earliest valid j is 1. That requires index >= lookback + 1.
        if (index < lookback + 1)
            return null;

        if (rsiValues[index] is null)
            return null;

        var currentRsi = rsiValues[index]!.Value;
        var currentClose = closes[index];

        var bullish = false;
        var bearish = false;

        for (var j = index - lookback; j <= index - 1; j++)
        {
            if (j < 1 || j >= closes.Count - 1)
                continue;

            if (!rsiValues[j].HasValue)
                continue;

            var pivotClose = closes[j];
            var pivotRsi = rsiValues[j]!.Value;

            // Confirmed swing low: both neighbours are strictly higher.
            // Bullish divergence: price makes a lower low while RSI makes a higher low.
            if (closes[j - 1] > closes[j] && closes[j] < closes[j + 1])
            {
                if (currentClose < pivotClose && currentRsi > pivotRsi)
                    bullish = true;
            }

            // Confirmed swing high: both neighbours are strictly lower.
            // Bearish divergence: price makes a higher high while RSI makes a lower high.
            if (closes[j - 1] < closes[j] && closes[j] > closes[j + 1])
            {
                if (currentClose > pivotClose && currentRsi < pivotRsi)
                    bearish = true;
            }
        }

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
