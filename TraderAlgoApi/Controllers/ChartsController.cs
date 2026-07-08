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
    // Upper bound on how many candles a single read can pull back, so an arbitrarily large
    // `lookback` (or a wide date range) can't materialise a runaway result set. Negative/zero
    // values are clamped up to 1.
    private const int MaxLookback = 5_000;
    private const int MaxDateIntervalCandles = 50_000;

    [HttpGet("candles")]
    public async Task<ActionResult<IReadOnlyList<CandleResponseDto>>> GetCandles(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        [FromQuery] int lookback = DefaultLookback,
        CancellationToken cancellationToken = default)
    {
        lookback = Math.Clamp(lookback, 1, MaxLookback);
        var symbolId = await ResolveSymbolIdAsync(symbol, cancellationToken);
        var intervalId = await ResolveIntervalIdAsync(interval, cancellationToken);
        if (symbolId is null || intervalId is null)
            return Ok(Array.Empty<CandleResponseDto>());

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(kline => kline.SymbolId == symbolId.Value && kline.IntervalId == intervalId.Value)
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
        lookback = Math.Clamp(lookback, 1, MaxLookback);
        var symbolId = await ResolveSymbolIdAsync(symbol, cancellationToken);
        var intervalId = await ResolveIntervalIdAsync(interval, cancellationToken);
        if (symbolId is null || intervalId is null)
            return Ok(Array.Empty<CandleWithIndicatorsResponseDto>());

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(kline => kline.SymbolId == symbolId.Value && kline.IntervalId == intervalId.Value)
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
                kline.Macd!.Histogram,
                kline.Atr!.Period,
                kline.Atr!.TrueRange,
                kline.Atr!.AtrValue))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }

    [HttpGet("candles/indicators/date-interval")]
    public async Task<ActionResult<IReadOnlyList<CandleWithIndicatorsResponseDto>>> GetCandlesWithIndicatorsByDateInterval(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
            return BadRequest("'from' must not be after 'to'.");

        // Date-only inputs: start the window at midnight and end it at 23:59 of the chosen day.
        var fromInstant = new DateTimeOffset(from.Year, from.Month, from.Day, 0, 0, 0, TimeSpan.Zero);
        var toInstant = new DateTimeOffset(to.Year, to.Month, to.Day, 23, 59, 0, TimeSpan.Zero);

        var symbolId = await ResolveSymbolIdAsync(symbol, cancellationToken);
        var intervalId = await ResolveIntervalIdAsync(interval, cancellationToken);
        if (symbolId is null || intervalId is null)
            return Ok(Array.Empty<CandleWithIndicatorsResponseDto>());

        // Cap the window at the most-recent MaxDateIntervalCandles within [from, to] so an
        // over-wide range (e.g. years of 1m candles) can't materialise millions of rows. We take
        // from the newest end and re-order ascending for chart rendering.
        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(kline =>
                kline.SymbolId == symbolId.Value &&
                kline.IntervalId == intervalId.Value &&
                kline.OpenTime >= fromInstant &&
                kline.OpenTime <= toInstant)
            .OrderByDescending(kline => kline.OpenTime)
            .Take(MaxDateIntervalCandles)
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
                kline.Macd!.Histogram,
                kline.Atr!.Period,
                kline.Atr!.TrueRange,
                kline.Atr!.AtrValue))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }

    // Resolve the symbol/interval to its primary key up front. Filtering KlineData by the
    // indexed SymbolId/IntervalId columns (rather than the Symbol.Code/Interval.Code navigation
    // properties) lets Postgres seek the (SymbolId, IntervalId, OpenTime) index directly — the
    // "latest N by OpenTime" then becomes a backward index scan with no sort or wide join.
    private async Task<int?> ResolveSymbolIdAsync(string? symbol, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(symbol)
            ? dbContext.Symbols.Where(s => s.IsDefault)
            : dbContext.Symbols.Where(s => s.Code == symbol);

        return await query.Select(s => (int?)s.Id).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> ResolveIntervalIdAsync(string? interval, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(interval)
            ? dbContext.Intervals.Where(i => i.IsDefault)
            : dbContext.Intervals.Where(i => i.Code == interval);

        return await query.Select(i => (int?)i.Id).FirstOrDefaultAsync(cancellationToken);
    }
}
