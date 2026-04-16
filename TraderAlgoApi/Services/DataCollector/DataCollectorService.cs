using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;
using TraderAlgoApi.Services.Binance;

namespace TraderAlgoApi.Services.DataCollector;

public sealed class DataCollectorService(
    ApplicationDbContext dbContext,
    IBinanceMarketDataService binanceMarketDataService,
    TimeProvider timeProvider) : IDataCollectorService
{
    private const int BinanceMaxKlineLimit = 1000;

    public async Task<DataCollectionResult> CollectKlinesAsync(
        string symbolCode,
        string intervalCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalCode);

        var symbol = await dbContext.Symbols
            .SingleAsync(
                symbol => symbol.Code == symbolCode.Trim().ToUpperInvariant(),
                cancellationToken);

        var interval = await dbContext.Intervals
            .SingleAsync(
                interval => interval.Code == intervalCode.Trim(),
                cancellationToken);

        var startTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var today = timeProvider.GetUtcNow().Date;
        var endTimeExclusive = new DateTimeOffset(today, TimeSpan.Zero);

        if (endTimeExclusive <= startTime)
        {
            return new DataCollectionResult(
                symbol.Code,
                interval.Code,
                startTime,
                endTimeExclusive,
                FetchedCount: 0,
                InsertedCount: 0,
                SkippedCount: 0);
        }

        var fetchedCount = 0;
        var insertedCount = 0;
        var skippedCount = 0;
        var cursor = startTime;
        var endTimeInclusive = endTimeExclusive.AddMilliseconds(-1);

        while (cursor < endTimeExclusive)
        {
            var klines = await binanceMarketDataService.getKlines(
                symbol.Code,
                interval.Code,
                cursor,
                endTimeInclusive,
                BinanceMaxKlineLimit,
                cancellationToken);

            var eligibleKlines = klines
                .Where(kline => kline.OpenTime >= startTime && kline.OpenTime < endTimeExclusive)
                .OrderBy(kline => kline.OpenTime)
                .ToArray();

            if (eligibleKlines.Length == 0)
            {
                break;
            }

            fetchedCount += eligibleKlines.Length;

            var openTimes = eligibleKlines
                .Select(kline => kline.OpenTime)
                .ToArray();

            var existingOpenTimes = await dbContext.KlineData
                .Where(kline =>
                    kline.SymbolId == symbol.Id &&
                    kline.IntervalId == interval.Id &&
                    openTimes.Contains(kline.OpenTime))
                .Select(kline => kline.OpenTime)
                .ToListAsync(cancellationToken);

            var existingOpenTimeSet = existingOpenTimes.ToHashSet();
            var newKlines = eligibleKlines
                .Where(kline => !existingOpenTimeSet.Contains(kline.OpenTime))
                .Select(kline => ToKlineData(kline, symbol.Id, interval.Id))
                .ToArray();

            skippedCount += eligibleKlines.Length - newKlines.Length;

            if (newKlines.Length > 0)
            {
                dbContext.KlineData.AddRange(newKlines);
                insertedCount += await dbContext.SaveChangesAsync(cancellationToken);
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
            endTimeExclusive,
            fetchedCount,
            insertedCount,
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
}
