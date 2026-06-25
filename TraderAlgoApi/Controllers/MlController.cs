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
    IMlflowTrackingRepository mlflowTrackingRepository,
    TimeProvider timeProvider,
    ILogger<MlController> logger) : ControllerBase
{
    [HttpPost("train")]
    public async Task<ActionResult<MlTrainStartedResponse>> Train(
        [FromBody] MlStartTrainingRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.MlPolicies
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .FirstOrDefaultAsync(p => p.Id == request.MlPolicyId, cancellationToken);
        if (policy is null)
            return NotFound($"Policy {request.MlPolicyId} not found.");

        if (request.From > request.To)
            return BadRequest("'from' must not be after 'to'.");

        // Date-only inputs: start the window at midnight and end it at 23:59 of the chosen day.
        var from = new DateTimeOffset(request.From.Year, request.From.Month, request.From.Day, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(request.To.Year, request.To.Month, request.To.Day, 23, 59, 0, TimeSpan.Zero);

        var now = timeProvider.GetUtcNow();

        // Record the run up front so the client can poll/stream it and Python can call back on completion.
        var run = new MlTrainingRun
        {
            MlPolicyId = policy.Id,
            From       = from,
            To         = to,
            StartedAt  = now,
            StatusEnum = MlTrainingRunStatus.Pending
        };
        dbContext.MlTrainingRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Build the Python training request from the policy's configuration.
        var forwarded = new MlTrainRequest(
            MlPolicyId:    policy.Id,
            TrainingRunId: run.Id,
            Symbol:        policy.Symbol.Code,
            Interval:      policy.Interval.Code,
            FromDate:      from.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ToDate:        to.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ModelId:       policy.Id.ToString(),
            TotalTimesteps:                policy.TotalTimesteps,
            InitialBalance:                policy.InitialBalance,
            Quantity:                      policy.Quantity,
            StopLoss:                      policy.StopLoss,
            TakeProfit:                    policy.TakeProfit,
            Breakeven:                     policy.Breakeven,
            BreakevenStop:                 policy.BreakevenStop,
            MaxCandlesPerTrade:            policy.MaxCandlesPerTrade,
            DailyProfitTarget:             policy.DailyProfit,
            DailyDrawdownLimit:            policy.DailyDrawdownLimit,
            FeeRate:                       policy.Fee,
            SlippageRate:                  policy.Slippage,
            MaxTrailingDrawdownThreshold:  policy.MaxTrailingDrawdown);

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
            Status:        run.StatusEnum,
            Message:       $"Training run {run.Id} (policy {policy.Id}) started on " +
                           $"{policy.Symbol.Code}/{policy.Interval.Code} " +
                           $"({request.From:yyyy-MM-dd} -> {request.To:yyyy-MM-dd})."));
    }

    [HttpGet("training-runs")]
    public async Task<ActionResult<IReadOnlyList<MlTrainingRunResponse>>> GetTrainingRuns(
        CancellationToken cancellationToken)
    {
        var runs = await dbContext.MlTrainingRuns
            .AsNoTracking()
            .Include(r => r.Policy).ThenInclude(p => p.Symbol)
            .Include(r => r.Policy).ThenInclude(p => p.Interval)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);

        var tracking = await mlflowTrackingRepository.GetTrackingSummariesAsync(
            runs.Select(r => r.Id).ToArray(),
            cancellationToken);

        return Ok(runs.Select(r => ToDto(r, GetTrackingSummary(tracking, r.Id))).ToList());
    }

    [HttpGet("training-runs/{id:long}")]
    public async Task<ActionResult<MlTrainingRunResponse>> GetTrainingRun(
        long id,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.MlTrainingRuns
            .AsNoTracking()
            .Include(r => r.Policy).ThenInclude(p => p.Symbol)
            .Include(r => r.Policy).ThenInclude(p => p.Interval)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (run is null)
            return NotFound($"Training run {id} not found.");

        var tracking = await mlflowTrackingRepository.GetTrackingSummariesAsync([id], cancellationToken);
        return Ok(ToDto(run, GetTrackingSummary(tracking, id)));
    }

    [HttpGet("training-runs/{id:long}/tracking")]
    public async Task<ActionResult<MlflowTrainingTrackingResponse>> GetTrainingRunTracking(
        long id,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.MlTrainingRuns
            .AsNoTracking()
            .AnyAsync(r => r.Id == id, cancellationToken);
        if (!exists)
            return NotFound($"Training run {id} not found.");

        var tracking = await mlflowTrackingRepository.GetTrackingAsync(
            id,
            includeMetricHistory: true,
            cancellationToken);

        return Ok(tracking);
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
        var policy = await dbContext.MlPolicies
            .AsNoTracking()
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .FirstOrDefaultAsync(p => p.Id == request.MlPolicyId, cancellationToken);
        if (policy is null)
            return NotFound($"Policy {request.MlPolicyId} not found.");

        var symbolCode = string.IsNullOrWhiteSpace(request.Symbol)
            ? policy.Symbol.Code
            : request.Symbol;

        var intervalCode = string.IsNullOrWhiteSpace(request.Interval)
            ? policy.Interval.Code
            : request.Interval;

        if (!string.Equals(symbolCode, policy.Symbol.Code, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(intervalCode, policy.Interval.Code, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                $"Policy {policy.Id} is configured for {policy.Symbol.Code}/{policy.Interval.Code}; use the same symbol and interval.");
        }

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
            MlPolicyId:   policy.Id,
            Symbol:       symbolCode,
            Interval:     intervalCode,
            ModelId:      policy.Id.ToString(),
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
            InitialAccountBalance: policy.InitialBalance,
            CurrentAccountBalance: policy.InitialBalance,
            CurrentDailyPnl: 0m,
            CurrentDailyDrawdown: 0m,
            WinsInRow: 0,
            LossesInRow: 0,
            TradesTakenToday: 0,
            DailyProfitTargetReached: false,
            DailyDrawdownReached: false,
            LastTradePnl: 0m,
            LastTradeCloseReason: string.Empty,
            CandlesSinceLastTradeClosed: 0,
            ConfiguredStopLoss: policy.StopLoss,
            ConfiguredTakeProfit: policy.TakeProfit,
            ConfiguredBreakeven: policy.Breakeven,
            ConfiguredBreakevenStop: policy.BreakevenStop,
            ConfiguredMaxCandlesPerTrade: policy.MaxCandlesPerTrade,
            FeeRate: policy.Fee,
            UnrealizedPnl: 0m);

        var result = await mlConnector.DecideAsync(decideRequest, cancellationToken);
        return Ok(result);
    }

    // -------------------------------------------------------------------------

    private static MlTrainingRunResponse ToDto(
        MlTrainingRun r,
        MlflowTrainingTrackingSummaryDto tracking) =>
        new(
            Id:             r.Id,
            MlPolicyId:     r.MlPolicyId,
            SymbolCode:     r.Policy.Symbol.Code,
            IntervalCode:   r.Policy.Interval.Code,
            From:           r.From.ToUnixTimeSeconds(),
            To:             r.To.ToUnixTimeSeconds(),
            StartedAt:      r.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    r.CompletedAt != null ? r.CompletedAt.Value.ToUnixTimeMilliseconds() : null,
            Status:         r.StatusEnum,
            TotalTimesteps: r.Policy.TotalTimesteps,
            FinalBalance:   r.FinalBalance,
            PnlPct:         r.PnlPct,
            NTrades:        r.NTrades,
            RunId:          r.RunId,
            Tracking:       tracking);

    private static MlflowTrainingTrackingSummaryDto GetTrackingSummary(
        IReadOnlyDictionary<long, MlflowTrainingTrackingSummaryDto> summaries,
        long trainingRunId) =>
        summaries.TryGetValue(trainingRunId, out var summary)
            ? summary
            : MlflowTrainingTrackingSummaryDto.Unavailable("Tracking data not available yet.");
}
