using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;

namespace TraderAlgoApi.Services.Rules;

public sealed class TradingRuleContextService(ApplicationDbContext dbContext) : ITradingRuleContextService
{
    public async Task<TradingRuleContext?> GetLatestContextAsync(
        string symbolCode,
        string intervalCode,
        CancellationToken cancellationToken = default)
    {
        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.Symbol.Code == symbolCode && k.Interval.Code == intervalCode)
            .Include(k => k.SimpleMovingAverage)
            .Include(k => k.RelativeStrengthIndex)
            .Include(k => k.Macd)
            .OrderByDescending(k => k.OpenTime)
            .Take(2)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        if (candles.Count < 2)
            return null;

        var previous = candles[0];
        var current = candles[1];

        return new TradingRuleContext(
            SymbolCode: symbolCode,
            IntervalCode: intervalCode,
            CurrentOpen: current.Open,
            CurrentHigh: current.High,
            CurrentLow: current.Low,
            CurrentClose: current.Close,
            PreviousClose: previous.Close,
            CurrentSma20: current.SimpleMovingAverage?.Sma20,
            CurrentSma100: current.SimpleMovingAverage?.Sma100,
            PreviousSma20: previous.SimpleMovingAverage?.Sma20,
            PreviousSma100: previous.SimpleMovingAverage?.Sma100,
            CurrentRsi: current.RelativeStrengthIndex?.Rsi,
            CurrentRsiSmooth: current.RelativeStrengthIndex?.RsiSmooth,
            PreviousRsi: previous.RelativeStrengthIndex?.Rsi,
            PreviousRsiSmooth: previous.RelativeStrengthIndex?.RsiSmooth,
            CurrentMacdLine: current.Macd?.MacdLine,
            CurrentSignalLine: current.Macd?.SignalLine,
            CurrentHistogram: current.Macd?.Histogram,
            PreviousHistogram: previous.Macd?.Histogram);
    }
}
