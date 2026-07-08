using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Dtos.Kronos;

namespace TraderAlgoApi.Services.Kronos;

public sealed class KronosPredictService(
    ApplicationDbContext dbContext,
    IKronosConnectorService kronosConnector) : IKronosPredictService
{
    public async Task<IReadOnlyList<CandleResponseDto>> PredictAsync(
        string symbol,
        string interval,
        KronosPredictOptions options,
        CancellationToken cancellationToken = default)
    {
        // Resolve the symbol (id + code) and interval id up front so the "latest N candles" read
        // seeks the (SymbolId, IntervalId, OpenTime) index directly rather than joining through the
        // Symbol/Interval navigations. The code is still needed for the Kronos request payload.
        var symbolQuery = string.IsNullOrWhiteSpace(symbol)
            ? dbContext.Symbols.Where(s => s.IsDefault)
            : dbContext.Symbols.Where(s => s.Code == symbol);
        var resolvedSymbol = await symbolQuery
            .Select(s => new { s.Id, s.Code })
            .FirstOrDefaultAsync(cancellationToken);

        var intervalQuery = string.IsNullOrWhiteSpace(interval)
            ? dbContext.Intervals.Where(i => i.IsDefault)
            : dbContext.Intervals.Where(i => i.Code == interval);
        var intervalId = await intervalQuery
            .Select(i => (int?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (resolvedSymbol is null || intervalId is null)
            return [];

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == resolvedSymbol.Id && k.IntervalId == intervalId.Value)
            .OrderByDescending(k => k.OpenTime)
            .Take(options.Lookback)
            .OrderBy(k => k.OpenTime)
            .Select(k => new KronosCandleDto(k.OpenTime, k.Open, k.High, k.Low, k.Close, k.Volume))
            .ToListAsync(cancellationToken);

        var request = new KronosPredictRequest(
            Symbol: resolvedSymbol.Code,
            ModelId: options.ModelId,
            Candles: candles,
            PredLen: options.PredLen,
            Temperature: options.Temperature,
            TopK: options.TopK,
            TopP: options.TopP,
            SampleCount: options.SampleCount);

        var response = await kronosConnector.PredictAsync(request, cancellationToken);

        return response.Predictions
            .Select(c => new CandleResponseDto(
                c.Timestamp.ToUnixTimeSeconds(),
                c.Open,
                c.High,
                c.Low,
                c.Close,
                c.Volume,
                BuyVolume: 0,
                SellVolume: 0))
            .ToList();
    }
}
