using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;
using TraderAlgoApi.Services.Binance;

namespace TraderAlgoApi.Services.DataCollector;

public sealed class DataCollectorService(
    ApplicationDbContext dbContext,
    IBinanceMarketDataService binanceMarketDataService) : IDataCollectorService
{
    private const int BinanceMaxKlineLimit = 1000;

    public async Task<DataCollectionResult> CollectKlinesAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset startTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalCode);

        var symbol = await dbContext.Symbols
            .SingleAsync(s => s.Code == symbolCode, cancellationToken);

        var interval = await dbContext.Intervals
            .SingleAsync(i => i.Code == intervalCode, cancellationToken);

        var fetchedCount = 0;
        var insertedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var cursor = startTime;
        DateTimeOffset? latestCandleOpenTime = null;

        while (true)
        {
            var klines = await binanceMarketDataService.GetKlinesAsync(
                symbol.Code,
                interval.Code,
                cursor,
                endTime: null,
                BinanceMaxKlineLimit,
                cancellationToken);

            var eligibleKlines = klines
                .Where(kline => kline.OpenTime >= startTime)
                .OrderBy(kline => kline.OpenTime)
                .ToArray();

            if (eligibleKlines.Length == 0)
            {
                break;
            }

            fetchedCount += eligibleKlines.Length;
            latestCandleOpenTime = eligibleKlines[^1].OpenTime;

            var openTimes = eligibleKlines
                .Select(kline => kline.OpenTime)
                .ToArray();

            var existingKlines = await dbContext.KlineData
                .Where(kline =>
                    kline.SymbolId == symbol.Id &&
                    kline.IntervalId == interval.Id &&
                    openTimes.Contains(kline.OpenTime))
                .ToListAsync(cancellationToken);

            var existingKlinesByOpenTime = existingKlines.ToDictionary(kline => kline.OpenTime);
            var newKlines = eligibleKlines
                .Where(kline => !existingKlinesByOpenTime.ContainsKey(kline.OpenTime))
                .Select(kline => ToKlineData(kline, symbol.Id, interval.Id))
                .ToArray();

            var changedCount = 0;
            foreach (var sourceKline in eligibleKlines)
            {
                if (!existingKlinesByOpenTime.TryGetValue(sourceKline.OpenTime, out var existingKline))
                {
                    continue;
                }

                if (ApplyChanges(existingKline, sourceKline))
                {
                    changedCount++;
                }
            }

            skippedCount += eligibleKlines.Length - newKlines.Length - changedCount;

            if (newKlines.Length > 0)
            {
                dbContext.KlineData.AddRange(newKlines);
                insertedCount += newKlines.Length;
            }

            if (newKlines.Length > 0 || changedCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                updatedCount += changedCount;
            }

            var nextCursor = eligibleKlines[^1].OpenTime.Add(interval.Duration);
            if (nextCursor <= cursor)
            {
                break;
            }

            cursor = nextCursor;
        }

        return new DataCollectionResult(
            symbol.Code,
            interval.Code,
            startTime,
            latestCandleOpenTime?.Add(interval.Duration) ?? startTime,
            latestCandleOpenTime,
            fetchedCount,
            insertedCount,
            updatedCount,
            skippedCount);
    }

    private static KlineData ToKlineData(BinanceKline kline, int symbolId, int intervalId)
    {
        return new KlineData
        {
            SymbolId = symbolId,
            IntervalId = intervalId,
            OpenTime = kline.OpenTime.ToUniversalTime(),
            CloseTime = kline.CloseTime.ToUniversalTime(),
            Open = kline.Open,
            High = kline.High,
            Low = kline.Low,
            Close = kline.Close,
            Volume = kline.Volume,
            QuoteAssetVolume = kline.QuoteAssetVolume,
            NumberOfTrades = kline.NumberOfTrades,
            TakerBuyBaseAssetVolume = kline.TakerBuyBaseAssetVolume,
            TakerBuyQuoteAssetVolume = kline.TakerBuyQuoteAssetVolume,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static bool ApplyChanges(KlineData target, BinanceKline source)
    {
        var hasChanges = false;

        hasChanges |= SetIfChanged(target.CloseTime, source.CloseTime.ToUniversalTime(), value => target.CloseTime = value);
        hasChanges |= SetIfChanged(target.Open, source.Open, value => target.Open = value);
        hasChanges |= SetIfChanged(target.High, source.High, value => target.High = value);
        hasChanges |= SetIfChanged(target.Low, source.Low, value => target.Low = value);
        hasChanges |= SetIfChanged(target.Close, source.Close, value => target.Close = value);
        hasChanges |= SetIfChanged(target.Volume, source.Volume, value => target.Volume = value);
        hasChanges |= SetIfChanged(target.QuoteAssetVolume, source.QuoteAssetVolume, value => target.QuoteAssetVolume = value);
        hasChanges |= SetIfChanged(target.NumberOfTrades, source.NumberOfTrades, value => target.NumberOfTrades = value);
        hasChanges |= SetIfChanged(target.TakerBuyBaseAssetVolume, source.TakerBuyBaseAssetVolume, value => target.TakerBuyBaseAssetVolume = value);
        hasChanges |= SetIfChanged(target.TakerBuyQuoteAssetVolume, source.TakerBuyQuoteAssetVolume, value => target.TakerBuyQuoteAssetVolume = value);

        return hasChanges;
    }

    private static bool SetIfChanged<T>(T currentValue, T newValue, Action<T> setValue)
        where T : IEquatable<T>
    {
        if (currentValue.Equals(newValue))
        {
            return false;
        }

        setValue(newValue);
        return true;
    }
}
