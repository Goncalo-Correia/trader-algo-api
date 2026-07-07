using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;

namespace TraderAlgoApi.Services.Indicators;

public sealed class IndicatorSyncService(
    ApplicationDbContext dbContext,
    ISimpleMovingAverageService smaService,
    IRsiService rsiService,
    IMacdService macdService,
    IAtrService atrService,
    ILogger<IndicatorSyncService> logger) : IIndicatorSyncService
{
    // Wilder's ATR averaging period; persisted per-row so the value is self-describing.
    private const int AtrPeriod = 14;

    // Candles processed per window. Caps peak memory (close/RSI/MACD arrays, existing-row
    // dictionaries and the EF change tracker are all sized to a window, not the whole range),
    // so a multi-million-candle recompute stays flat in memory instead of OOM-ing.
    private const int WindowSize = 5000;

    // 200 candles gives Wilder's RSI-14 smoothing well below 0.1% residual error,
    // and comfortably covers the SMA-100 look-back window. Each window is re-seeded with this
    // much prior context, so windowing matches the single-pass result within that tolerance.
    private const int ContextSize = 200;

    public async Task<IndicatorSyncResult> FullSyncPairAsync(
        Symbol symbol,
        Interval interval,
        CancellationToken cancellationToken = default)
    {
        var from = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
            .MinAsync(k => (DateTimeOffset?)k.OpenTime, cancellationToken);

        if (from is null)
            return new IndicatorSyncResult(symbol.Code, interval.Code, 0, 0, 0);

        var to = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
            .MaxAsync(k => k.OpenTime, cancellationToken);

        return await SyncRangeAsync(symbol, interval, from.Value, to, cancellationToken);
    }

    public async Task<IndicatorSyncResult> PartialSyncPairAsync(
        Symbol symbol,
        Interval interval,
        CancellationToken cancellationToken = default)
    {
        // Find the earliest candle missing any indicator row.
        // Querying each indicator separately is more efficient than a multi-OR join,
        // and ensures partial sync catches newly added indicator types automatically.
        var fromSma = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id
                && !dbContext.SimpleMovingAverages.Any(sma => sma.KlineDataId == k.Id))
            .OrderBy(k => k.OpenTime)
            .Select(k => (DateTimeOffset?)k.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);

        var fromRsi = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id
                && !dbContext.RelativeStrengthIndexes.Any(rsi => rsi.KlineDataId == k.Id))
            .OrderBy(k => k.OpenTime)
            .Select(k => (DateTimeOffset?)k.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);

        var fromMacd = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id
                && !dbContext.Macd.Any(m => m.KlineDataId == k.Id))
            .OrderBy(k => k.OpenTime)
            .Select(k => (DateTimeOffset?)k.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);

        var fromAtr = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id
                && !dbContext.Atrs.Any(a => a.KlineDataId == k.Id))
            .OrderBy(k => k.OpenTime)
            .Select(k => (DateTimeOffset?)k.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);

        var candidates = new[] { fromSma, fromRsi, fromMacd, fromAtr }
            .Where(dt => dt.HasValue)
            .Select(dt => dt!.Value)
            .ToList();

        var from = candidates.Count > 0 ? (DateTimeOffset?)candidates.Min() : null;

        if (from is null)
            return new IndicatorSyncResult(symbol.Code, interval.Code, 0, 0, 0);

        var to = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
            .MaxAsync(k => k.OpenTime, cancellationToken);

        return await SyncRangeAsync(symbol, interval, from.Value, to, cancellationToken);
    }

    public async Task ComputeAndSaveAsync(
        int symbolId,
        int intervalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var (inserted, updated, skipped) = await SyncRangeInternalAsync(
            symbolId, intervalId, from, to, cancellationToken);

        if (inserted > 0 || updated > 0)
        {
            logger.LogDebug(
                "Indicators computed for symbolId={SymbolId}/intervalId={IntervalId} [{From}..{To}]: " +
                "{Inserted} inserted, {Updated} updated, {Skipped} skipped",
                symbolId, intervalId, from, to, inserted, updated, skipped);
        }
    }

    private async Task<IndicatorSyncResult> SyncRangeAsync(
        Symbol symbol,
        Interval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Syncing indicators for {Symbol}/{Interval} [{From}..{To}]",
            symbol.Code, interval.Code, from, to);

        var (inserted, updated, skipped) = await SyncRangeInternalAsync(
            symbol.Id, interval.Id, from, to, cancellationToken);

        logger.LogDebug(
            "Indicators synced for {Symbol}/{Interval}: {Inserted} inserted, {Updated} updated, {Skipped} skipped",
            symbol.Code, interval.Code, inserted, updated, skipped);

        return new IndicatorSyncResult(symbol.Code, interval.Code, inserted, updated, skipped);
    }

    private async Task<(int Inserted, int Updated, int Skipped)> SyncRangeInternalAsync(
        int symbolId,
        int intervalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var insertedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;

        // Walk the range in bounded windows. OpenTimes are unique per symbol/interval, so we
        // advance with an exclusive lower bound (> the last window's final candle) after the
        // first (inclusive) window.
        var cursor = from;
        var inclusive = true;

        while (true)
        {
            var windowQuery = dbContext.KlineData
                .AsNoTracking()
                .Where(k => k.SymbolId == symbolId && k.IntervalId == intervalId && k.OpenTime <= to);

            windowQuery = inclusive
                ? windowQuery.Where(k => k.OpenTime >= cursor)
                : windowQuery.Where(k => k.OpenTime > cursor);

            var targetKlines = await windowQuery
                .OrderBy(k => k.OpenTime)
                .Take(WindowSize)
                .Select(k => new { k.Id, k.OpenTime, k.High, k.Low, k.Close })
                .ToListAsync(cancellationToken);

            if (targetKlines.Count == 0)
                break;

            var windowFirstOpenTime = targetKlines[0].OpenTime;

            // Prior candles for look-back context (SMA-100 needs 99; RSI-14/ATR-14 Wilder's need ~200).
            // Re-seeded per window so each algorithm's state is continuous within the window.
            var contextCandles = await dbContext.KlineData
                .AsNoTracking()
                .Where(k => k.SymbolId == symbolId && k.IntervalId == intervalId && k.OpenTime < windowFirstOpenTime)
                .OrderByDescending(k => k.OpenTime)
                .Take(ContextSize)
                .OrderBy(k => k.OpenTime)
                .Select(k => new { k.High, k.Low, k.Close })
                .ToListAsync(cancellationToken);

            var allHighs  = contextCandles.Select(c => c.High).Concat(targetKlines.Select(t => t.High)).ToList();
            var allLows   = contextCandles.Select(c => c.Low).Concat(targetKlines.Select(t => t.Low)).ToList();
            var allCloses = contextCandles.Select(c => c.Close).Concat(targetKlines.Select(t => t.Close)).ToList();
            var contextCount = contextCandles.Count;

            var allRsiValues = rsiService.ComputeAll(allCloses);
            var allMacdValues = macdService.ComputeAll(allCloses);
            var allAtrValues = atrService.ComputeAll(allHighs, allLows, allCloses, AtrPeriod);

            var targetIds = targetKlines.Select(t => t.Id).ToList();

            // Load existing rows for this window with tracking so EF detects mutations.
            var existingSmas = await dbContext.SimpleMovingAverages
                .Where(sma => targetIds.Contains(sma.KlineDataId))
                .ToDictionaryAsync(sma => sma.KlineDataId, cancellationToken);

            var existingRsis = await dbContext.RelativeStrengthIndexes
                .Where(rsi => targetIds.Contains(rsi.KlineDataId))
                .ToDictionaryAsync(rsi => rsi.KlineDataId, cancellationToken);

            var existingMacds = await dbContext.Macd
                .Where(m => targetIds.Contains(m.KlineDataId))
                .ToDictionaryAsync(m => m.KlineDataId, cancellationToken);

            var existingAtrs = await dbContext.Atrs
                .Where(a => targetIds.Contains(a.KlineDataId))
                .ToDictionaryAsync(a => a.KlineDataId, cancellationToken);

            for (var i = 0; i < targetKlines.Count; i++)
            {
                var target = targetKlines[i];
                var targetIdx = contextCount + i;

                // ── SMA ─────────────────────────────────────────────────────────────
                var (sma20, sma100) = smaService.Compute(allCloses, targetIdx);

                if (existingSmas.TryGetValue(target.Id, out var existingSma))
                {
                    if (existingSma.Sma20 == sma20 && existingSma.Sma100 == sma100)
                    {
                        skippedCount++;
                    }
                    else
                    {
                        existingSma.Sma20 = sma20;
                        existingSma.Sma100 = sma100;
                        updatedCount++;
                    }
                }
                else
                {
                    dbContext.SimpleMovingAverages.Add(new SimpleMovingAverage
                    {
                        KlineDataId = target.Id,
                        Sma20 = sma20,
                        Sma100 = sma100
                    });
                    insertedCount++;
                }

                // ── RSI ─────────────────────────────────────────────────────────────
                var rsi = allRsiValues[targetIdx];
                var rsiSmooth = rsiService.ComputeSmooth(allRsiValues, targetIdx);
                var divergence = rsiService.DetectDivergence(allCloses, allRsiValues, targetIdx);

                if (existingRsis.TryGetValue(target.Id, out var existingRsi))
                {
                    if (existingRsi.Rsi == rsi && existingRsi.RsiSmooth == rsiSmooth && existingRsi.Divergence == divergence)
                    {
                        skippedCount++;
                    }
                    else
                    {
                        existingRsi.Rsi = rsi;
                        existingRsi.RsiSmooth = rsiSmooth;
                        existingRsi.Divergence = divergence;
                        updatedCount++;
                    }
                }
                else
                {
                    dbContext.RelativeStrengthIndexes.Add(new RelativeStrengthIndex
                    {
                        KlineDataId = target.Id,
                        Rsi = rsi,
                        RsiSmooth = rsiSmooth,
                        Divergence = divergence
                    });
                    insertedCount++;
                }

                // ── MACD ────────────────────────────────────────────────────────────
                var (macdLine, signalLine, histogram) = allMacdValues[targetIdx];

                if (existingMacds.TryGetValue(target.Id, out var existingMacd))
                {
                    if (existingMacd.MacdLine == macdLine && existingMacd.SignalLine == signalLine && existingMacd.Histogram == histogram)
                    {
                        skippedCount++;
                    }
                    else
                    {
                        existingMacd.MacdLine = macdLine;
                        existingMacd.SignalLine = signalLine;
                        existingMacd.Histogram = histogram;
                        updatedCount++;
                    }
                }
                else
                {
                    dbContext.Macd.Add(new Macd
                    {
                        KlineDataId = target.Id,
                        MacdLine = macdLine,
                        SignalLine = signalLine,
                        Histogram = histogram
                    });
                    insertedCount++;
                }

                // ── ATR ─────────────────────────────────────────────────────────────
                var (trueRange, atr) = allAtrValues[targetIdx];

                if (existingAtrs.TryGetValue(target.Id, out var existingAtr))
                {
                    if (existingAtr.Period == AtrPeriod && existingAtr.TrueRange == trueRange && existingAtr.AtrValue == atr)
                    {
                        skippedCount++;
                    }
                    else
                    {
                        existingAtr.Period = AtrPeriod;
                        existingAtr.TrueRange = trueRange;
                        existingAtr.AtrValue = atr;
                        updatedCount++;
                    }
                }
                else
                {
                    dbContext.Atrs.Add(new Atr
                    {
                        KlineDataId = target.Id,
                        Period = AtrPeriod,
                        TrueRange = trueRange,
                        AtrValue = atr
                    });
                    insertedCount++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Detach this window's tracked entities so the change tracker stays flat across the
            // whole range instead of growing to millions of entries.
            dbContext.ChangeTracker.Clear();

            if (targetKlines.Count < WindowSize)
                break;

            cursor = targetKlines[^1].OpenTime;
            inclusive = false;
        }

        return (insertedCount, updatedCount, skippedCount);
    }
}
