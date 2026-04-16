using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/charts")]
public sealed class ChartsController(ApplicationDbContext dbContext) : ControllerBase
{
    private const string DefaultSymbol = "BTC/USD";
    private const string DefaultInterval = "1h";
    private const int CandleLimit = 100;

    [HttpGet("candles")]
    public async Task<ActionResult<IReadOnlyList<CandleResponseDto>>> GetCandles(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeSymbol(string.IsNullOrWhiteSpace(symbol) ? DefaultSymbol : symbol);
        var normalizedInterval = NormalizeInterval(interval);

        var query = dbContext.KlineData
            .AsNoTracking()
            .Where(kline =>
                kline.Symbol.Code == normalizedSymbol &&
                kline.Interval.Code == normalizedInterval);

        var candles = await query
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

    private static string NormalizeSymbol(string symbol)
    {
        return symbol
            .Trim()
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static string NormalizeInterval(string? interval)
    {
        return string.IsNullOrWhiteSpace(interval)
            ? DefaultInterval
            : interval.Trim().ToLowerInvariant();
    }
}
