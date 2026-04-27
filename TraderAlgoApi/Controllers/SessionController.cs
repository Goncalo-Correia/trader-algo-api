using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Session;
using TraderAlgoApi.Services.Session;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController(
    ApplicationDbContext dbContext,
    NyseSessionService sessionService,
    TimeProvider timeProvider) : ControllerBase
{
    private const int DefaultBuckets = 30;

    [HttpGet("current")]
    public Task<ActionResult<SessionOhlcvDto>> GetCurrent(
        [FromQuery] string? symbol,
        CancellationToken cancellationToken)
    {
        var window = sessionService.CurrentSession(timeProvider.GetUtcNow());
        return GetSession(symbol, window, cancellationToken);
    }

    [HttpGet("previous")]
    public Task<ActionResult<SessionOhlcvDto>> GetPrevious(
        [FromQuery] string? symbol,
        CancellationToken cancellationToken)
    {
        var window = sessionService.PreviousSession(timeProvider.GetUtcNow());
        return GetSession(symbol, window, cancellationToken);
    }

    [HttpGet("volume-profile")]
    public async Task<ActionResult<IReadOnlyList<VolumeProfileLevelDto>>> GetVolumeProfile(
        [FromQuery] string? symbol,
        [FromQuery] int buckets = DefaultBuckets,
        CancellationToken cancellationToken = default)
    {
        if (buckets <= 0)
            return BadRequest("buckets must be greater than zero.");

        var symbolCode = await ResolveSymbolCodeAsync(symbol, cancellationToken);
        var window = sessionService.CurrentSession(timeProvider.GetUtcNow());

        var klines = await dbContext.KlineData
            .AsNoTracking()
            .Where(k =>
                k.Symbol.Code == symbolCode &&
                k.Interval.Code == "1m" &&
                k.OpenTime >= window.Start &&
                k.OpenTime < window.End)
            .Select(k => new
            {
                k.High,
                k.Low,
                k.Close,
                k.Volume,
                k.TakerBuyBaseAssetVolume
            })
            .ToListAsync(cancellationToken);

        if (klines.Count == 0)
            return NotFound();

        var sessionHigh = klines.Max(k => k.High);
        var sessionLow  = klines.Min(k => k.Low);
        var priceRange  = sessionHigh - sessionLow;

        // When all candles share the same price (edge case), put everything in one bucket.
        if (priceRange == 0)
        {
            var total = new VolumeProfileLevelDto(
                PriceFrom: sessionLow,
                PriceTo:   sessionHigh,
                Volume:    klines.Sum(k => k.Volume),
                BuyVolume: klines.Sum(k => k.TakerBuyBaseAssetVolume));

            return Ok(new[] { total });
        }

        var bucketWidth = priceRange / buckets;
        var volumes    = new decimal[buckets];
        var buyVolumes = new decimal[buckets];

        foreach (var k in klines)
        {
            // Clamp so that a close exactly at sessionHigh lands in the last bucket.
            var index = (int)((k.Close - sessionLow) / bucketWidth);
            index = Math.Clamp(index, 0, buckets - 1);

            volumes[index]    += k.Volume;
            buyVolumes[index] += k.TakerBuyBaseAssetVolume;
        }

        var levels = Enumerable.Range(0, buckets)
            .Select(i => new VolumeProfileLevelDto(
                PriceFrom: sessionLow + i       * bucketWidth,
                PriceTo:   sessionLow + (i + 1) * bucketWidth,
                Volume:    volumes[i],
                BuyVolume: buyVolumes[i]))
            .ToList();

        return Ok(levels);
    }

    private async Task<ActionResult<SessionOhlcvDto>> GetSession(
        string? symbol,
        (DateTimeOffset Start, DateTimeOffset End) window,
        CancellationToken cancellationToken)
    {
        var symbolCode = await ResolveSymbolCodeAsync(symbol, cancellationToken);

        var klines = await dbContext.KlineData
            .AsNoTracking()
            .Where(k =>
                k.Symbol.Code == symbolCode &&
                k.Interval.Code == "1m" &&
                k.OpenTime >= window.Start &&
                k.OpenTime < window.End)
            .OrderBy(k => k.OpenTime)
            .Select(k => new { k.Open, k.High, k.Low, k.Close, k.Volume })
            .ToListAsync(cancellationToken);

        if (klines.Count == 0)
            return NotFound();

        var dto = new SessionOhlcvDto(
            Open:         klines[0].Open,
            High:         klines.Max(k => k.High),
            Low:          klines.Min(k => k.Low),
            Close:        klines[^1].Close,
            Volume:       klines.Sum(k => k.Volume),
            SessionStart: window.Start.ToUnixTimeMilliseconds(),
            SessionEnd:   window.End.ToUnixTimeMilliseconds());

        return Ok(dto);
    }

    private async Task<string> ResolveSymbolCodeAsync(string? symbol, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(symbol)
            ? await dbContext.Symbols
                .Where(s => s.IsDefault)
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : symbol;
}
