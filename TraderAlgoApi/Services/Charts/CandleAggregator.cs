using System.Collections.Concurrent;

namespace TraderAlgoApi.Services.Charts;

/// <summary>
/// Singleton that tracks the current in-progress (partial) candle for every
/// symbol/interval combination. The streaming service updates it on every price
/// tick; live chart clients read from it to send the latest partial bar state.
/// </summary>
public sealed class CandleAggregator
{
    // Key: "{symbolCode}:{intervalCode}"
    private readonly ConcurrentDictionary<string, PartialCandle> _candles = new();

    public void OnTick(string symbol, string intervalCode, decimal price, DateTimeOffset now)
    {
        var key      = MakeKey(symbol, intervalCode);
        var duration = IntervalDuration(intervalCode);
        var barStart = BarStartTime(now, duration);

        _candles.AddOrUpdate(
            key,
            addValue: new PartialCandle(barStart, price, price, price, price, 0m),
            updateValueFactory: (_, existing) =>
            {
                // If the tick belongs to a newer bar, reset.
                if (barStart > existing.OpenTime)
                    return new PartialCandle(barStart, price, price, price, price, 0m);

                return existing with
                {
                    High  = Math.Max(existing.High, price),
                    Low   = Math.Min(existing.Low,  price),
                    Close = price,
                };
            });
    }

    /// <summary>
    /// Called when a candle closes. Replaces the partial candle with the authoritative
    /// closed values, preventing the next tick from re-using stale OHLC.
    /// </summary>
    public void OnCandleClosed(
        string symbol, string intervalCode,
        DateTimeOffset openTime, decimal open, decimal high, decimal low, decimal close, decimal volume)
    {
        var key = MakeKey(symbol, intervalCode);
        _candles[key] = new PartialCandle(openTime, open, high, low, close, volume);
    }

    public PartialCandle? GetPartial(string symbol, string intervalCode) =>
        _candles.TryGetValue(MakeKey(symbol, intervalCode), out var c) ? c : null;

    private static string MakeKey(string symbol, string intervalCode) =>
        $"{symbol}:{intervalCode}";

    private static DateTimeOffset BarStartTime(DateTimeOffset now, TimeSpan duration)
    {
        var ticks = now.UtcTicks / duration.Ticks;
        return new DateTimeOffset(ticks * duration.Ticks, TimeSpan.Zero);
    }

    private static TimeSpan IntervalDuration(string intervalCode) => intervalCode switch
    {
        "1m"  => TimeSpan.FromMinutes(1),
        "5m"  => TimeSpan.FromMinutes(5),
        "15m" => TimeSpan.FromMinutes(15),
        "1h"  => TimeSpan.FromHours(1),
        "4h"  => TimeSpan.FromHours(4),
        "1d"  => TimeSpan.FromDays(1),
        _     => TimeSpan.FromMinutes(1),
    };
}

public sealed record PartialCandle(
    DateTimeOffset OpenTime,
    decimal        Open,
    decimal        High,
    decimal        Low,
    decimal        Close,
    decimal        Volume);
