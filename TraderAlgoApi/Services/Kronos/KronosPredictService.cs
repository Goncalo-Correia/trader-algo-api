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
        var symbolCode = string.IsNullOrWhiteSpace(symbol)
            ? await dbContext.Symbols
                .Where(s => s.IsDefault)
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : symbol;

        var intervalCode = string.IsNullOrWhiteSpace(interval)
            ? await dbContext.Intervals
                .Where(i => i.IsDefault)
                .Select(i => i.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : interval;

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.Symbol.Code == symbolCode && k.Interval.Code == intervalCode)
            .OrderByDescending(k => k.OpenTime)
            .Take(options.Lookback)
            .OrderBy(k => k.OpenTime)
            .Select(k => new KronosCandleDto(k.OpenTime, k.Open, k.High, k.Low, k.Close, k.Volume))
            .ToListAsync(cancellationToken);

        var request = new KronosPredictRequest(
            Symbol: symbolCode,
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
