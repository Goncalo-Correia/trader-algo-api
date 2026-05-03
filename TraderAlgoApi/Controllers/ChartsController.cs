using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/charts")]
public sealed class ChartsController(ApplicationDbContext dbContext) : ControllerBase
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
                kline.Volume,
                kline.TakerBuyBaseAssetVolume,
                kline.Volume - kline.TakerBuyBaseAssetVolume))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }

    [HttpGet("candles/indicators")]
    public async Task<ActionResult<IReadOnlyList<CandleWithIndicatorsResponseDto>>> GetCandlesWithIndicators(
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
            .Select(kline => new CandleWithIndicatorsResponseDto(
                kline.OpenTime.ToUnixTimeSeconds(),
                kline.Open,
                kline.High,
                kline.Low,
                kline.Close,
                kline.Volume,
                kline.TakerBuyBaseAssetVolume,
                kline.Volume - kline.TakerBuyBaseAssetVolume,
                kline.SimpleMovingAverage!.Sma20,
                kline.SimpleMovingAverage!.Sma100,
                kline.RelativeStrengthIndex!.Rsi,
                kline.RelativeStrengthIndex!.RsiSmooth,
                kline.RelativeStrengthIndex!.Divergence,
                kline.Macd!.MacdLine,
                kline.Macd!.SignalLine,
                kline.Macd!.Histogram))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }
}
