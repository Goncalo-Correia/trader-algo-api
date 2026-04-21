using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Services.Charts;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/charts")]
public sealed class ChartsController(ApplicationDbContext dbContext, IChartsService chartsService) : ControllerBase
{
    private const int CandleLimit = 100;

    [HttpGet("candles")]
    public async Task<ActionResult<IReadOnlyList<CandleResponseDto>>> GetCandles(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken)
    {
        var resolvedSymbol = string.IsNullOrWhiteSpace(symbol)
            ? await dbContext.Symbols
                .Where(s => s.IsDefault)
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : symbol;

        var normalizedSymbol = chartsService.NormalizeSymbol(resolvedSymbol);
        var normalizedInterval = chartsService.NormalizeInterval(interval);

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(kline =>
                kline.Symbol.Code == normalizedSymbol &&
                kline.Interval.Code == normalizedInterval)
            .OrderByDescending(kline => kline.OpenTime)
            .Take(CandleLimit)
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
