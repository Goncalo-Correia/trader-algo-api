using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Dtos.Kronos;
using TraderAlgoApi.Services.Kronos;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/charts")]
public sealed class ChartsController(
    ApplicationDbContext dbContext,
    IKronosConnectorService kronosConnector) : ControllerBase
{
    private const int DefaultLookback = 100;

    [HttpGet("candles")]
    public async Task<ActionResult<IReadOnlyList<CandleResponseDto>>> GetCandles(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        [FromQuery] int lookback = DefaultLookback,
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
            .Where(kline =>
                kline.Symbol.Code == symbolCode &&
                kline.Interval.Code == intervalCode)
            .OrderByDescending(kline => kline.OpenTime)
            .Take(lookback)
            .OrderBy(kline => kline.OpenTime)
            .Select(kline => new CandleResponseDto(
                kline.OpenTime.ToUnixTimeSeconds(),
                kline.Open,
                kline.High,
                kline.Low,
                kline.Close,
                kline.Volume))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }

    [HttpGet("predict")]
    public async Task<ActionResult<IReadOnlyList<CandleResponseDto>>> Predict(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        [FromQuery] int lookback = DefaultLookback,
        [FromQuery] string modelId = "kronos-mini",
        [FromQuery] int predLen = 10,
        [FromQuery] double temperature = 1.0,
        [FromQuery] int topK = 0,
        [FromQuery] double topP = 0.9,
        [FromQuery] int sampleCount = 1,
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

        var historicalCandles = await dbContext.KlineData
            .AsNoTracking()
            .Where(kline =>
                kline.Symbol.Code == symbolCode &&
                kline.Interval.Code == intervalCode)
            .OrderByDescending(kline => kline.OpenTime)
            .Take(lookback)
            .OrderBy(kline => kline.OpenTime)
            .Select(kline => new KronosCandleDto(
                kline.OpenTime,
                kline.Open,
                kline.High,
                kline.Low,
                kline.Close,
                kline.Volume))
            .ToListAsync(cancellationToken);

        var request = new KronosPredictRequest(
            Symbol: symbolCode,
            ModelId: modelId,
            Candles: historicalCandles,
            PredLen: predLen,
            Temperature: temperature,
            TopK: topK,
            TopP: topP,
            SampleCount: sampleCount);

        var kronosResponse = await kronosConnector.PredictAsync(request, cancellationToken);

        var predictions = kronosResponse.Predictions
            .Select(c => new CandleResponseDto(
                c.Timestamp.ToUnixTimeSeconds(),
                c.Open,
                c.High,
                c.Low,
                c.Close,
                c.Volume))
            .ToList();

        return Ok(predictions);
    }
}
