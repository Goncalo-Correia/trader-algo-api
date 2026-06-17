using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Services.Kronos;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/kronos")]
public sealed class KronosController(IKronosPredictService predictService) : ControllerBase
{
    private const int PredLen = 10;

    // kronos-mini — max 2048 candles

    [HttpGet("mini/precise")]
    public Task<ActionResult<IReadOnlyList<CandleResponseDto>>> MiniPrecise(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken) =>
        PredictAsync(symbol, interval, new KronosPredictOptions(
            ModelId: "kronos-mini",
            Lookback: 2048,
            PredLen: PredLen,
            Temperature: 0.5,
            TopK: 50,
            TopP: 0.7,
            SampleCount: 10), cancellationToken);

    [HttpGet("mini/diverse")]
    public Task<ActionResult<IReadOnlyList<CandleResponseDto>>> MiniDiverse(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken) =>
        PredictAsync(symbol, interval, new KronosPredictOptions(
            ModelId: "kronos-mini",
            Lookback: 2048,
            PredLen: PredLen,
            Temperature: 1.0,
            TopK: 0,
            TopP: 0.9,
            SampleCount: 1), cancellationToken);

    // kronos-small — max 512 candles

    [HttpGet("small/precise")]
    public Task<ActionResult<IReadOnlyList<CandleResponseDto>>> SmallPrecise(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken) =>
        PredictAsync(symbol, interval, new KronosPredictOptions(
            ModelId: "kronos-small",
            Lookback: 512,
            PredLen: PredLen,
            Temperature: 0.5,
            TopK: 50,
            TopP: 0.7,
            SampleCount: 10), cancellationToken);

    [HttpGet("small/diverse")]
    public Task<ActionResult<IReadOnlyList<CandleResponseDto>>> SmallDiverse(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken) =>
        PredictAsync(symbol, interval, new KronosPredictOptions(
            ModelId: "kronos-small",
            Lookback: 512,
            PredLen: PredLen,
            Temperature: 1.0,
            TopK: 0,
            TopP: 0.9,
            SampleCount: 1), cancellationToken);

    // kronos-base — max 512 candles

    [HttpGet("base/precise")]
    public Task<ActionResult<IReadOnlyList<CandleResponseDto>>> BasePrecise(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken) =>
        PredictAsync(symbol, interval, new KronosPredictOptions(
            ModelId: "kronos-base",
            Lookback: 512,
            PredLen: PredLen,
            Temperature: 0.5,
            TopK: 50,
            TopP: 0.7,
            SampleCount: 10), cancellationToken);

    [HttpGet("base/diverse")]
    public Task<ActionResult<IReadOnlyList<CandleResponseDto>>> BaseDiverse(
        [FromQuery] string? symbol,
        [FromQuery] string? interval,
        CancellationToken cancellationToken) =>
        PredictAsync(symbol, interval, new KronosPredictOptions(
            ModelId: "kronos-base",
            Lookback: 512,
            PredLen: PredLen,
            Temperature: 1.0,
            TopK: 0,
            TopP: 0.9,
            SampleCount: 1), cancellationToken);

    private async Task<ActionResult<IReadOnlyList<CandleResponseDto>>> PredictAsync(
        string? symbol,
        string? interval,
        KronosPredictOptions options,
        CancellationToken cancellationToken)
    {
        // HttpRequestException (Kronos unavailable) is mapped to 503 by the global exception handler.
        var predictions = await predictService.PredictAsync(
            symbol ?? string.Empty,
            interval ?? string.Empty,
            options,
            cancellationToken);

        return Ok(predictions);
    }
}
