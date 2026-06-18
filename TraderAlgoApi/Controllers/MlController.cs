using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Ml;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Ml;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/ml")]
public sealed class MlController(
    ApplicationDbContext dbContext,
    IMlConnectorService mlConnector,
    TimeProvider timeProvider,
    ILogger<MlController> logger) : ControllerBase
{
    [HttpPost("train")]
    public async Task<ActionResult<MlTrainStartedResponse>> Train(
        [FromBody] MlTrainRequest request,
        CancellationToken cancellationToken)
    {
        var symbol = await dbContext.Symbols
            .FirstOrDefaultAsync(s => s.Code == request.Symbol, cancellationToken);
        if (symbol is null)
            return NotFound($"Symbol '{request.Symbol}' not found.");

        var interval = await dbContext.Intervals
            .FirstOrDefaultAsync(i => i.Code == request.Interval, cancellationToken);
        if (interval is null)
            return NotFound($"Interval '{request.Interval}' not found.");

        if (!TryParseDate(request.FromDate, out var from) || !TryParseDate(request.ToDate, out var to))
            return BadRequest($"Could not parse date range '{request.FromDate}' .. '{request.ToDate}'.");

        if (from >= to)
            return BadRequest("'from_date' must be earlier than 'to_date'.");

        var now = timeProvider.GetUtcNow();

        // Record the run up front so the client can poll/stream it and Python can call back on completion.
        var run = new MlTrainingRun
        {
            ModelId        = request.ModelId,
            SymbolId       = symbol.Id,
            IntervalId     = interval.Id,
            From           = from,
            To             = to,
            StartedAt      = now,
            StatusEnum     = MlTrainingRunStatus.Pending,
            TotalTimesteps = request.TotalTimesteps
        };
        dbContext.MlTrainingRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Forward to Python with the run id so its completion webhook can find this row.
        var forwarded = request with
        {
            Symbol        = symbol.Code,
            Interval      = interval.Code,
            TrainingRunId = run.Id
        };

        try
        {
            await mlConnector.TrainAsync(forwarded, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start training run {RunId} on the ML service", run.Id);
            run.StatusEnum = MlTrainingRunStatus.Failed;
            run.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "ML service is unavailable.", detail = ex.Message, trainingRunId = run.Id });
        }

        return Ok(new MlTrainStartedResponse(
            TrainingRunId: run.Id,
            ModelId:       run.ModelId,
            Status:        run.StatusEnum,
            Message:       $"Training run {run.Id} ('{run.ModelId}') started on " +
                           $"{symbol.Code}/{interval.Code} ({request.FromDate} -> {request.ToDate})."));
    }

    [HttpGet("training-runs")]
    public async Task<ActionResult<IReadOnlyList<MlTrainingRunResponse>>> GetTrainingRuns(
        CancellationToken cancellationToken)
    {
        var runs = await dbContext.MlTrainingRuns
            .AsNoTracking()
            .Include(r => r.Symbol)
            .Include(r => r.Interval)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);

        return Ok(runs.Select(ToDto).ToList());
    }

    [HttpGet("training-runs/{id:long}")]
    public async Task<ActionResult<MlTrainingRunResponse>> GetTrainingRun(
        long id,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.MlTrainingRuns
            .AsNoTracking()
            .Include(r => r.Symbol)
            .Include(r => r.Interval)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return run is null ? NotFound($"Training run {id} not found.") : Ok(ToDto(run));
    }

    /// <summary>
    /// Webhook invoked by the Python ML service when a training run finishes or fails.
    /// Transitions the run out of Pending/Running and records the final metrics.
    /// </summary>
    [HttpPatch("training-runs/{id:long}/complete")]
    public async Task<IActionResult> CompleteTrainingRun(
        long id,
        [FromBody] MlTrainingRunCompleteRequest request,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.MlTrainingRuns
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (run is null)
            return NotFound($"Training run {id} not found.");

        if (!Enum.TryParse<MlTrainingRunStatus>(request.Status, ignoreCase: true, out var status))
            return BadRequest($"Unknown status '{request.Status}'.");

        run.StatusEnum = status;
        run.FinalBalance = request.FinalBalance ?? run.FinalBalance;
        run.PnlPct = request.PnlPct ?? run.PnlPct;
        run.NTrades = request.NTrades ?? run.NTrades;
        run.RunId = request.RunId ?? run.RunId;

        if (status is MlTrainingRunStatus.Completed or MlTrainingRunStatus.Failed)
            run.CompletedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("training-runs/{id:long}/decisions")]
    public async Task<ActionResult<MlTrainingDecisionsResponse>> GetTrainingDecisions(
        long id,
        CancellationToken cancellationToken)
    {
        var decisions = await mlConnector.GetTrainingDecisionsAsync(id, cancellationToken);
        return decisions is null
            ? NotFound($"No training decision log for run {id}.")
            : Ok(decisions);
    }

    [HttpDelete("training-runs/{id:long}")]
    public async Task<IActionResult> DeleteTrainingRun(long id, CancellationToken cancellationToken)
    {
        var run = await dbContext.MlTrainingRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (run is null)
            return NotFound($"Training run {id} not found.");

        // Remove the decision log on the ML service first (best-effort), then the DB record.
        await mlConnector.DeleteTrainingDecisionsAsync(id, cancellationToken);

        dbContext.MlTrainingRuns.Remove(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("decide")]
    public async Task<ActionResult<MlDecideResponse>> Decide(
        [FromBody] MlDecideQueryRequest request,
        CancellationToken cancellationToken)
    {
        var symbolCode = string.IsNullOrWhiteSpace(request.Symbol)
            ? await dbContext.Symbols
                .Where(s => s.IsDefault)
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : request.Symbol;

        var intervalCode = string.IsNullOrWhiteSpace(request.Interval)
            ? await dbContext.Intervals
                .Where(i => i.IsDefault)
                .Select(i => i.Code)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : request.Interval;

        var kline = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.Symbol.Code == symbolCode && k.Interval.Code == intervalCode)
            .OrderByDescending(k => k.OpenTime)
            .Select(k => new
            {
                k.Open,
                k.High,
                k.Low,
                k.Close,
                k.Volume,
                k.TakerBuyBaseAssetVolume,
                Sma20    = k.SimpleMovingAverage != null ? k.SimpleMovingAverage.Sma20    : (decimal?)null,
                Sma100   = k.SimpleMovingAverage != null ? k.SimpleMovingAverage.Sma100   : (decimal?)null,
                Rsi      = k.RelativeStrengthIndex != null ? k.RelativeStrengthIndex.Rsi      : (decimal?)null,
                RsiSmooth = k.RelativeStrengthIndex != null ? k.RelativeStrengthIndex.RsiSmooth : (decimal?)null,
                MacdLine  = k.Macd != null ? k.Macd.MacdLine   : (decimal?)null,
                SignalLine = k.Macd != null ? k.Macd.SignalLine : (decimal?)null,
                Histogram  = k.Macd != null ? k.Macd.Histogram : (decimal?)null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (kline is null)
            return NotFound($"No candle data found for {symbolCode}/{intervalCode}.");

        var decideRequest = new MlDecideRequest(
            Symbol:       symbolCode,
            Interval:     intervalCode,
            ModelId:      request.ModelId ?? "ppo-v1",
            Candle: new MlCandleFeatures(
                Open:           kline.Open,
                High:           kline.High,
                Low:            kline.Low,
                Close:          kline.Close,
                Volume:         kline.Volume,
                TakerBuyVolume: kline.TakerBuyBaseAssetVolume,
                Sma20:          kline.Sma20,
                Sma100:         kline.Sma100,
                Rsi:            kline.Rsi,
                RsiSmooth:      kline.RsiSmooth,
                MacdLine:       kline.MacdLine,
                SignalLine:     kline.SignalLine,
                Histogram:      kline.Histogram),
            Position:      0,
            CandlesHeld:   0,
            UnrealizedPnl: 0m);

        var result = await mlConnector.DecideAsync(decideRequest, cancellationToken);
        return Ok(result);
    }

    // -------------------------------------------------------------------------

    private static bool TryParseDate(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out result);

    private static MlTrainingRunResponse ToDto(MlTrainingRun r) =>
        new(
            Id:             r.Id,
            ModelId:        r.ModelId,
            SymbolCode:     r.Symbol.Code,
            IntervalCode:   r.Interval.Code,
            From:           r.From.ToUnixTimeSeconds(),
            To:             r.To.ToUnixTimeSeconds(),
            StartedAt:      r.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    r.CompletedAt != null ? r.CompletedAt.Value.ToUnixTimeMilliseconds() : null,
            Status:         r.StatusEnum,
            TotalTimesteps: r.TotalTimesteps,
            FinalBalance:   r.FinalBalance,
            PnlPct:         r.PnlPct,
            NTrades:        r.NTrades,
            RunId:          r.RunId);
}
