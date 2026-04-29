using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;

namespace TraderAlgoApi.Services.Indicators;

public sealed class IndicatorSyncService(
    ApplicationDbContext dbContext,
    ISimpleMovingAverageService smaService,
    IRsiService rsiService,
    IMacdService macdService,
    ILogger<IndicatorSyncService> logger) : IIndicatorSyncService
{
    private const int SaveChunkSize = 500;

    // 200 candles gives Wilder's RSI-14 smoothing well below 0.1% residual error,
    // and comfortably covers the SMA-100 look-back window.
    private const int ContextSize = 200;

    public async Task<IReadOnlyList<IndicatorSyncResult>> FullSyncAsync(CancellationToken cancellationToken = default)
    {
        var symbols = await dbContext.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        var intervals = await dbContext.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Duration)
            .ToListAsync(cancellationToken);

        var results = new List<IndicatorSyncResult>();

        foreach (var symbol in symbols)
        {
            foreach (var interval in intervals)
            {
                var from = await dbContext.KlineData
                    .AsNoTracking()
                    .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
                    .MinAsync(k => (DateTimeOffset?)k.OpenTime, cancellationToken);

                if (from is null)
                {
                    results.Add(new IndicatorSyncResult(symbol.Code, interval.Code, 0, 0, 0));
                    continue;
                }

                var to = await dbContext.KlineData
                    .AsNoTracking()
                    .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
                    .MaxAsync(k => k.OpenTime, cancellationToken);

                var result = await SyncRangeAsync(symbol, interval, from.Value, to, cancellationToken);
                results.Add(result);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<IndicatorSyncResult>> PartialSyncAsync(CancellationToken cancellationToken = default)
    {
        var symbols = await dbContext.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        var intervals = await dbContext.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Duration)
            .ToListAsync(cancellationToken);

        var results = new List<IndicatorSyncResult>();

        foreach (var symbol in symbols)
        {
            foreach (var interval in intervals)
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

                var candidates = new[] { fromSma, fromRsi, fromMacd }
                    .Where(dt => dt.HasValue)
                    .Select(dt => dt!.Value)
                    .ToList();

                var from = candidates.Count > 0 ? (DateTimeOffset?)candidates.Min() : null;

                if (from is null)
                {
                    results.Add(new IndicatorSyncResult(symbol.Code, interval.Code, 0, 0, 0));
                    continue;
                }

                var to = await dbContext.KlineData
                    .AsNoTracking()
                    .Where(k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id)
                    .MaxAsync(k => k.OpenTime, cancellationToken);

                var result = await SyncRangeAsync(symbol, interval, from.Value, to, cancellationToken);
                results.Add(result);
            }
        }

        return results;
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
        logger.LogInformation(
            "Syncing indicators for {Symbol}/{Interval} [{From}..{To}]",
            symbol.Code, interval.Code, from, to);

        var (inserted, updated, skipped) = await SyncRangeInternalAsync(
            symbol.Id, interval.Id, from, to, cancellationToken);

        logger.LogInformation(
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
        // Load prior candles for look-back context (SMA-100 needs 99; RSI-14 Wilder's needs ~200).
        var contextCloses = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbolId && k.IntervalId == intervalId && k.OpenTime < from)
            .OrderByDescending(k => k.OpenTime)
            .Take(ContextSize)
            .OrderBy(k => k.OpenTime)
            .Select(k => k.Close)
            .ToListAsync(cancellationToken);

        var targetKlines = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == symbolId && k.IntervalId == intervalId
                && k.OpenTime >= from && k.OpenTime <= to)
            .OrderBy(k => k.OpenTime)
            .Select(k => new { k.Id, k.Close })
            .ToListAsync(cancellationToken);

        if (targetKlines.Count == 0)
            return (0, 0, 0);

        var allCloses = contextCloses.Concat(targetKlines.Select(t => t.Close)).ToList();
        var contextCount = contextCloses.Count;

        // Compute all indicators in one pass over the combined close-price list so each
        // algorithm's internal state (Wilder smoothing, EMA carry) is continuous.
        var allRsiValues = rsiService.ComputeAll(allCloses);
        var allMacdValues = macdService.ComputeAll(allCloses);

        var targetIds = targetKlines.Select(t => t.Id).ToList();

        // Load existing rows with tracking so EF detects mutations.
        var existingSmas = await dbContext.SimpleMovingAverages
            .Where(sma => targetIds.Contains(sma.KlineDataId))
            .ToDictionaryAsync(sma => sma.KlineDataId, cancellationToken);

        var existingRsis = await dbContext.RelativeStrengthIndexes
            .Where(rsi => targetIds.Contains(rsi.KlineDataId))
            .ToDictionaryAsync(rsi => rsi.KlineDataId, cancellationToken);

        var existingMacds = await dbContext.Macd
            .Where(m => targetIds.Contains(m.KlineDataId))
            .ToDictionaryAsync(m => m.KlineDataId, cancellationToken);

        var insertedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var pendingCount = 0;

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
                    pendingCount++;
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
                pendingCount++;
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
                    pendingCount++;
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
                pendingCount++;
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
                    pendingCount++;
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
                pendingCount++;
            }

            if (pendingCount >= SaveChunkSize)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                pendingCount = 0;
            }
        }

        if (pendingCount > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return (insertedCount, updatedCount, skippedCount);
    }
}
