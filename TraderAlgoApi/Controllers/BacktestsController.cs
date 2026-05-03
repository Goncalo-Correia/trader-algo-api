using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Backtests;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Services.Backtests;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/backtests")]
public sealed class BacktestsController(
    IBacktestService backtestService,
    ApplicationDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BacktestSummaryResponseDto>> Create(
        [FromBody] CreateBacktestRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await backtestService.CreateAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)       { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BacktestSummaryResponseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await backtestService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BacktestDetailResponseDto>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await backtestService.GetByIdAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("candles")]
    public async Task<ActionResult<IReadOnlyList<CandleResponseDto>>> GetCandles(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (from >= to)
            return BadRequest("'from' must be earlier than 'to'.");

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.Symbol.Code == symbol &&
                        k.Interval.Code == interval &&
                        k.OpenTime >= from &&
                        k.OpenTime <= to)
            .OrderBy(k => k.OpenTime)
            .Select(k => new CandleResponseDto(
                k.OpenTime.ToUnixTimeSeconds(),
                k.Open,
                k.High,
                k.Low,
                k.Close,
                k.Volume,
                k.TakerBuyBaseAssetVolume,
                k.Volume - k.TakerBuyBaseAssetVolume))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }

    [HttpGet("candles/indicators")]
    public async Task<ActionResult<IReadOnlyList<CandleWithIndicatorsResponseDto>>> GetCandlesWithIndicators(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (from >= to)
            return BadRequest("'from' must be earlier than 'to'.");

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.Symbol.Code == symbol &&
                        k.Interval.Code == interval &&
                        k.OpenTime >= from &&
                        k.OpenTime <= to)
            .OrderBy(k => k.OpenTime)
            .Select(k => new CandleWithIndicatorsResponseDto(
                k.OpenTime.ToUnixTimeSeconds(),
                k.Open,
                k.High,
                k.Low,
                k.Close,
                k.Volume,
                k.TakerBuyBaseAssetVolume,
                k.Volume - k.TakerBuyBaseAssetVolume,
                k.SimpleMovingAverage!.Sma20,
                k.SimpleMovingAverage!.Sma100,
                k.RelativeStrengthIndex!.Rsi,
                k.RelativeStrengthIndex!.RsiSmooth,
                k.RelativeStrengthIndex!.Divergence,
                k.Macd!.MacdLine,
                k.Macd!.SignalLine,
                k.Macd!.Histogram))
            .ToListAsync(cancellationToken);

        return Ok(candles);
    }
}
