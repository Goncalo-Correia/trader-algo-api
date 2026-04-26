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
                kline.Volume))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }
}
