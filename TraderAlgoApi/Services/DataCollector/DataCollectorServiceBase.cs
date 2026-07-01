using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.MarketData;

namespace TraderAlgoApi.Services.DataCollector;

public class DataCollectorServiceBase(
    ApplicationDbContext dbContext,
    IMarketDataProvider provider,
    IIndicatorSyncService indicatorSyncService) : IDataCollectorService
{
    public async Task<DataCollectionResult> CollectKlinesAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset startTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalCode);

        var symbol   = await dbContext.Symbols.SingleAsync(s => s.Code == symbolCode, cancellationToken);
        var interval = await dbContext.Intervals.SingleAsync(i => i.Code == intervalCode, cancellationToken);

        var errors = new List<DataCollectionError>();
        void AddError(DateTimeOffset? candleOpenTime, string message) =>
            errors.Add(new DataCollectionError(symbol.Code, interval.Code, candleOpenTime, message));

        var fetchedCount  = 0;
        var insertedCount = 0;
        var updatedCount  = 0;
        var skippedCount  = 0;
        var cursor = startTime;
        DateTimeOffset? latestCandleOpenTime = null;

        while (true)
        {
            var candles = await provider.GetCandlesAsync(
                symbol.Code,
                interval.Code,
                cursor,
                endTime: null,
                provider.MaxPageSize,
                cancellationToken);

            var eligible = candles
                .Where(c => c.OpenTime >= startTime)
                .OrderBy(c => c.OpenTime)
                .ToArray();

            if (eligible.Length == 0)
                break;

            fetchedCount += eligible.Length;
            latestCandleOpenTime = eligible[^1].OpenTime;

            var openTimes = eligible.Select(c => c.OpenTime).ToArray();

            var existingKlines = await dbContext.KlineData
                .Where(k =>
                    k.SymbolId   == symbol.Id &&
                    k.IntervalId == interval.Id &&
                    openTimes.Contains(k.OpenTime))
                .ToListAsync(cancellationToken);

            var existingByOpenTime = existingKlines.ToDictionary(k => k.OpenTime);

            // Build inserts/updates one candle at a time so a single bad candle is recorded and
            // skipped instead of aborting the batch.
            var newKlines        = new List<KlineData>();
            var changedOpenTimes = new List<DateTimeOffset>();
            var erroredCount     = 0;

            foreach (var candle in eligible)
            {
                try
                {
                    if (existingByOpenTime.TryGetValue(candle.OpenTime, out var existing))
                    {
                        if (ApplyChanges(existing, candle))
                            changedOpenTimes.Add(existing.OpenTime);
                    }
                    else
                    {
                        newKlines.Add(ToKlineData(candle, symbol.Id, interval.Id));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    erroredCount++;
                    AddError(candle.OpenTime, ex.Message);
                }
            }

            var changedCount = changedOpenTimes.Count;
            skippedCount += eligible.Length - newKlines.Count - changedCount - erroredCount;

            if (newKlines.Count > 0 || changedCount > 0)
            {
                try
                {
                    if (newKlines.Count > 0)
                        dbContext.KlineData.AddRange(newKlines);

                    await dbContext.SaveChangesAsync(cancellationToken);
                    insertedCount += newKlines.Count;
                    updatedCount  += changedCount;

                    var allAffectedTimes = newKlines.Select(k => k.OpenTime).Concat(changedOpenTimes).ToList();
                    var indicatorFrom    = allAffectedTimes.Min();
                    var indicatorTo      = allAffectedTimes.Max();

                    if (changedOpenTimes.Count > 0)
                    {
                        var latestInDb = await dbContext.KlineData
                            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
                            .MaxAsync(k => k.OpenTime, cancellationToken);
                        if (latestInDb > indicatorTo)
                            indicatorTo = latestInDb;
                    }

                    await indicatorSyncService.ComputeAndSaveAsync(
                        symbol.Id, interval.Id, indicatorFrom, indicatorTo, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The persist covers the whole batch, so attribute the failure to every candle
                    // it touched, then stop paging this pair — the tracked context is now suspect.
                    foreach (var openTime in newKlines.Select(k => k.OpenTime).Concat(changedOpenTimes))
                        AddError(openTime, ex.Message);
                    break;
                }
            }

            // Detach this page's tracked candles (loaded existing + any inserts) so the change
            // tracker stays flat across a long backfill instead of accumulating every candle.
            dbContext.ChangeTracker.Clear();

            var nextCursor = eligible[^1].OpenTime.Add(interval.Duration);
            if (nextCursor <= cursor)
                break;

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
            skippedCount,
            errors);
    }

    public async Task<DataCollectionResult> SyncGapsAsync(
        string symbolCode,
        string intervalCode,
        DateTimeOffset windowStart,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalCode);

        var symbol   = await dbContext.Symbols.SingleAsync(s => s.Code == symbolCode, cancellationToken);
        var interval = await dbContext.Intervals.SingleAsync(i => i.Code == intervalCode, cancellationToken);

        var errors = new List<DataCollectionError>();
        void AddError(DateTimeOffset? candleOpenTime, string message) =>
            errors.Add(new DataCollectionError(symbol.Code, interval.Code, candleOpenTime, message));

        // Only inspect candles from the window floor forward, so the nightly timer can use a
        // short lookback instead of rescanning the entire history on every run.
        var openTimes = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id && k.OpenTime >= windowStart)
            .OrderBy(k => k.OpenTime)
            .Select(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        if (openTimes.Count == 0)
            return await CollectKlinesAsync(symbolCode, intervalCode, windowStart, cancellationToken);

        var gaps = new List<(DateTimeOffset Start, DateTimeOffset? End)>();

        // Leading gap: candles missing between the window floor and the first row in range.
        if (openTimes[0] > windowStart)
            gaps.Add((windowStart, openTimes[0]));

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
                FetchedCount: 0, InsertedCount: 0, UpdatedCount: 0, SkippedCount: 0,
                Errors: errors);
        }

        var fetchedCount         = 0;
        var insertedCount        = 0;
        DateTimeOffset? latestCandleOpenTime = openTimes[^1];

        foreach (var (gapStart, gapEnd) in gaps)
        {
            var cursor = gapStart;
            DateTimeOffset? firstInsertedInGap = null;

            while (true)
            {
                var candles = await provider.GetCandlesAsync(
                    symbol.Code, interval.Code,
                    cursor, gapEnd,
                    provider.MaxPageSize, cancellationToken);

                var eligible = candles
                    .Where(c => c.OpenTime >= gapStart && (gapEnd is null || c.OpenTime < gapEnd))
                    .OrderBy(c => c.OpenTime)
                    .ToArray();

                if (eligible.Length == 0)
                    break;

                fetchedCount += eligible.Length;

                var batchOpenTimes = eligible.Select(c => c.OpenTime).ToList();
                var existingTimes  = await dbContext.KlineData
                    .Where(k => k.SymbolId   == symbol.Id
                             && k.IntervalId == interval.Id
                             && batchOpenTimes.Contains(k.OpenTime))
                    .Select(k => k.OpenTime)
                    .ToHashSetAsync(cancellationToken);

                // Convert one candle at a time so a single malformed candle is recorded and
                // skipped rather than aborting the whole gap backfill.
                var toInsert = new List<KlineData>();
                foreach (var candle in eligible.Where(c => !existingTimes.Contains(c.OpenTime)))
                {
                    try
                    {
                        toInsert.Add(ToKlineData(candle, symbol.Id, interval.Id));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        AddError(candle.OpenTime, ex.Message);
                    }
                }

                if (toInsert.Count > 0)
                {
                    try
                    {
                        dbContext.KlineData.AddRange(toInsert);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        insertedCount      += toInsert.Count;
                        firstInsertedInGap ??= toInsert[0].OpenTime;

                        // Detach the just-persisted candles so the change tracker doesn't grow
                        // across the (potentially multi-million-candle) gap backfill.
                        dbContext.ChangeTracker.Clear();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // The persist covers the whole batch, so attribute the failure to every
                        // candle it touched, then stop backfilling this gap.
                        foreach (var kline in toInsert)
                            AddError(kline.OpenTime, ex.Message);
                        break;
                    }
                }

                if (eligible[^1].OpenTime > latestCandleOpenTime)
                    latestCandleOpenTime = eligible[^1].OpenTime;

                if (eligible.Length < provider.MaxPageSize)
                    break;

                var nextCursor = eligible[^1].OpenTime.Add(interval.Duration);
                if (gapEnd is not null && nextCursor >= gapEnd)
                    break;
                if (nextCursor <= cursor)
                    break;

                cursor = nextCursor;
            }

            if (firstInsertedInGap is not null)
            {
                var latestInDb = await dbContext.KlineData
                    .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
                    .MaxAsync(k => k.OpenTime, cancellationToken);

                await indicatorSyncService.ComputeAndSaveAsync(
                    symbol.Id, interval.Id,
                    firstInsertedInGap.Value, latestInDb,
                    cancellationToken);
            }
        }

        return new DataCollectionResult(
            symbol.Code, interval.Code,
            openTimes[0], latestCandleOpenTime!.Value.Add(interval.Duration),
            latestCandleOpenTime,
            fetchedCount, insertedCount, UpdatedCount: 0, SkippedCount: 0,
            Errors: errors);
    }

    private static KlineData ToKlineData(Candle candle, int symbolId, int intervalId) =>
        new()
        {
            SymbolId                 = symbolId,
            IntervalId               = intervalId,
            OpenTime                 = candle.OpenTime.ToUniversalTime(),
            CloseTime                = candle.CloseTime.ToUniversalTime(),
            Open                     = candle.Open,
            High                     = candle.High,
            Low                      = candle.Low,
            Close                    = candle.Close,
            Volume                   = candle.Volume,
            QuoteAssetVolume         = candle.QuoteAssetVolume,
            NumberOfTrades           = candle.NumberOfTrades,
            TakerBuyBaseAssetVolume  = candle.TakerBuyBaseVolume,
            TakerBuyQuoteAssetVolume = candle.TakerBuyQuoteVolume,
            CreatedAt                = DateTimeOffset.UtcNow,
        };

    private static bool ApplyChanges(KlineData target, Candle source)
    {
        var changed = false;

        changed |= SetIfChanged(target.CloseTime,                source.CloseTime.ToUniversalTime(),  v => target.CloseTime                = v);
        changed |= SetIfChanged(target.Open,                     source.Open,                         v => target.Open                     = v);
        changed |= SetIfChanged(target.High,                     source.High,                         v => target.High                     = v);
        changed |= SetIfChanged(target.Low,                      source.Low,                          v => target.Low                      = v);
        changed |= SetIfChanged(target.Close,                    source.Close,                        v => target.Close                    = v);
        changed |= SetIfChanged(target.Volume,                   source.Volume,                       v => target.Volume                   = v);
        changed |= SetIfChanged(target.QuoteAssetVolume,         source.QuoteAssetVolume,             v => target.QuoteAssetVolume         = v);
        changed |= SetIfChanged(target.NumberOfTrades,           source.NumberOfTrades,               v => target.NumberOfTrades           = v);
        changed |= SetIfChanged(target.TakerBuyBaseAssetVolume,  source.TakerBuyBaseVolume,           v => target.TakerBuyBaseAssetVolume  = v);
        changed |= SetIfChanged(target.TakerBuyQuoteAssetVolume, source.TakerBuyQuoteVolume,          v => target.TakerBuyQuoteAssetVolume = v);

        return changed;
    }

    private static bool SetIfChanged<T>(T current, T next, Action<T> set) where T : IEquatable<T>
    {
        if (current.Equals(next)) return false;
        set(next);
        return true;
    }
}
