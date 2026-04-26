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

    public async Task<DataCollectionResult> SyncGapsAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset fallbackStartTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalCode);

        var symbol = await dbContext.Symbols.SingleAsync(s => s.Code == symbolCode, cancellationToken);
        var interval = await dbContext.Intervals.SingleAsync(i => i.Code == intervalCode, cancellationToken);

        var openTimes = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
            .OrderBy(k => k.OpenTime)
            .Select(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        if (openTimes.Count == 0)
            return await CollectKlinesAsync(symbolCode, intervalCode, fallbackStartTime, cancellationToken);

        var gaps = new List<(DateTimeOffset Start, DateTimeOffset? End)>();

        for (var i = 0; i < openTimes.Count - 1; i++)
        {
            var expected = openTimes[i].Add(interval.Duration);
            if (openTimes[i + 1] > expected)
                gaps.Add((expected, openTimes[i + 1]));
        }

        var trailingStart = openTimes[^1].Add(interval.Duration);
        if (trailingStart < DateTimeOffset.UtcNow)
            gaps.Add((trailingStart, null));

        if (gaps.Count == 0)
        {
            return new DataCollectionResult(
                symbol.Code, interval.Code,
                openTimes[0], openTimes[^1].Add(interval.Duration),
                openTimes[^1],
                FetchedCount: 0, InsertedCount: 0, UpdatedCount: 0, SkippedCount: 0);
        }

        var fetchedCount = 0;
        var insertedCount = 0;
        DateTimeOffset? latestCandleOpenTime = openTimes[^1];

        foreach (var (gapStart, gapEnd) in gaps)
        {
            var cursor = gapStart;

            while (true)
            {
                var klines = await binanceMarketDataService.GetKlinesAsync(
                    symbol.Code, interval.Code,
                    cursor, gapEnd,
                    BinanceMaxKlineLimit, cancellationToken);

                var eligible = klines
                    .Where(k => k.OpenTime >= gapStart && (gapEnd is null || k.OpenTime < gapEnd))
                    .OrderBy(k => k.OpenTime)
                    .ToArray();

                if (eligible.Length == 0)
                    break;

                fetchedCount += eligible.Length;

                var toInsert = eligible
                    .Select(k => ToKlineData(k, symbol.Id, interval.Id))
                    .ToArray();

                dbContext.KlineData.AddRange(toInsert);
                await dbContext.SaveChangesAsync(cancellationToken);
                insertedCount += toInsert.Length;

                if (eligible[^1].OpenTime > latestCandleOpenTime)
                    latestCandleOpenTime = eligible[^1].OpenTime;

                if (eligible.Length < BinanceMaxKlineLimit)
                    break;

                var nextCursor = eligible[^1].OpenTime.Add(interval.Duration);
                if (gapEnd is not null && nextCursor >= gapEnd)
                    break;
                if (nextCursor <= cursor)
                    break;

                cursor = nextCursor;
            }
        }

        return new DataCollectionResult(
            symbol.Code, interval.Code,
            openTimes[0], latestCandleOpenTime!.Value.Add(interval.Duration),
            latestCandleOpenTime,
            fetchedCount, insertedCount, UpdatedCount: 0, SkippedCount: 0);
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
