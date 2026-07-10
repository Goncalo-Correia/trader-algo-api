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
    private const int MaxTrainingRunsListed = 500;

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
        var forwarded = BuildTrainRequest(policy, run.Id, from, to);

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
        [FromQuery] long? mlPolicyId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MlTrainingRuns
            .AsNoTracking()
            .Include(r => r.Policy).ThenInclude(p => p.Symbol)
            .Include(r => r.Policy).ThenInclude(p => p.Interval)
            .AsQueryable();

        if (mlPolicyId is long policyId)
            query = query.Where(r => r.MlPolicyId == policyId);

        // Cap the run list (most-recent first): every returned run also triggers an MLflow tracking
        // lookup, so an unbounded list would fan out into an unbounded number of tracking queries.
        var runs = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(MaxTrainingRunsListed)
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

        // Terminal states are final. Ignore late/duplicate callbacks (e.g. an orphan-recovery
        // 'Failed' arriving after a genuine 'Completed') so they can't clobber the recorded result.
        if (run.StatusEnum is MlTrainingRunStatus.Completed or MlTrainingRunStatus.Failed)
            return NoContent();

        run.StatusEnum = status;
        run.FinalBalance = request.FinalBalance ?? run.FinalBalance;
        run.PnlPct = request.PnlPct ?? run.PnlPct;
        run.FinalBalanceOos = request.FinalBalanceOos ?? run.FinalBalanceOos;
        run.PnlPctOos = request.PnlPctOos ?? run.PnlPctOos;
        run.NTrades = request.NTrades ?? run.NTrades;
        run.RunId = request.RunId ?? run.RunId;

        if (status is MlTrainingRunStatus.Completed or MlTrainingRunStatus.Failed)
            run.CompletedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("training-runs/{id:long}/decisions")]
    [Produces("application/json")]
    public async Task<IActionResult> GetTrainingDecisions(
        long id,
        CancellationToken cancellationToken)
    {
        // Served from the training_decisions telemetry table the sidecar writes (no longer
        // proxied to the Python service). Return the stored JSON directly; some logs are tens of
        // megabytes, so materializing the whole DTO just to serialize it again is wasteful.
        var payload = await dbContext.GetTrainingDecisionLogPayloadAsync(id, cancellationToken);
        return payload is null
            ? NotFound($"No training decision log for run {id}.")
            : Content(payload, "application/json");
    }

    [HttpDelete("training-runs/{id:long}")]
    public async Task<IActionResult> DeleteTrainingRun(long id, CancellationToken cancellationToken)
    {
        var run = await dbContext.MlTrainingRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (run is null)
            return NotFound($"Training run {id} not found.");

        // Drop the run's decision log (telemetry table is FK-free, so delete it explicitly),
        // then the run record itself.
        var key = id.ToString();
        await dbContext.TrainingDecisionLogs
            .Where(d => d.RunId == key)
            .ExecuteDeleteAsync(cancellationToken);

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
                k.OpenTime,
                k.Volume,
                k.TakerBuyBaseAssetVolume,
                Sma20    = k.SimpleMovingAverage != null ? k.SimpleMovingAverage.Sma20    : (decimal?)null,
                Sma100   = k.SimpleMovingAverage != null ? k.SimpleMovingAverage.Sma100   : (decimal?)null,
                Rsi      = k.RelativeStrengthIndex != null ? k.RelativeStrengthIndex.Rsi      : (decimal?)null,
                RsiSmooth = k.RelativeStrengthIndex != null ? k.RelativeStrengthIndex.RsiSmooth : (decimal?)null,
                MacdLine  = k.Macd != null ? k.Macd.MacdLine   : (decimal?)null,
                SignalLine = k.Macd != null ? k.Macd.SignalLine : (decimal?)null,
                Histogram  = k.Macd != null ? k.Macd.Histogram : (decimal?)null,
                Atr        = k.Atr != null ? k.Atr.AtrValue : (decimal?)null,
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
                Histogram:      kline.Histogram,
                Atr:            kline.Atr,
                OpenTime:       kline.OpenTime.ToUnixTimeSeconds()),
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
            ConfiguredMaxCandlesPerTrade: policy.MaxCandlesPerTrade,
            FeeRate: policy.Fee,
            UnrealizedPnl: 0m);

        var result = await mlConnector.DecideAsync(decideRequest, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists the currently-served model per policy. Promotion is gated and risk-aware, so a Completed
    /// training run is not necessarily the served model — use this to know what is actually live.
    /// Returns exactly one row per policy; <c>served</c> is false when nothing has been promoted yet.
    /// </summary>
    /// <remarks>
    /// Replaces the removed model-registry endpoint that used to live at <c>GET /api/ml/models</c>.
    /// </remarks>
    [HttpGet("served-models")]
    public async Task<ActionResult<IReadOnlyList<MlServedModelResponse>>> GetServedModels(
        CancellationToken cancellationToken)
    {
        var policies = await dbContext.MlPolicies
            .AsNoTracking()
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        var models = await mlConnector.GetModelsAsync(cancellationToken);
        var byPolicy = models
            .Where(m => m.MlPolicyId.HasValue)
            .GroupBy(m => m.MlPolicyId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = policies.Select(p =>
        {
            byPolicy.TryGetValue(p.Id, out var m);
            return new MlServedModelResponse(
                MlPolicyId:          p.Id,
                SymbolCode:          p.Symbol.Code,
                IntervalCode:        p.Interval.Code,
                Served:              m is not null,
                ServedTrainingRunId: m?.TrainingRunId,
                ModelId:             m?.ModelId,
                FinalBalance:        m?.FinalBalance,
                PnlPct:              m?.PnlPct,
                OosFinalBalance:     m?.OosFinalBalance,
                OosPnlPct:           m?.OosPnlPct,
                NTrades:             m?.NTrades,
                Calibrated:          m?.Calibrated,
                ObsDim:              m?.ObsDim,
                SchemaVersion:       m?.SchemaVersion,
                RunId:               m?.RunId);
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Kicks off a training run for every policy over a date range. Intended for the one-time,
    /// post-upgrade retrain required when the ML observation schema changes (models trained before
    /// the change are rejected at inference time until retrained).
    /// </summary>
    [HttpPost("retrain-all")]
    public async Task<ActionResult<IReadOnlyList<MlRetrainPolicyResult>>> RetrainAll(
        [FromBody] MlRetrainAllRequest request,
        CancellationToken cancellationToken)
    {
        if (request.From > request.To)
            return BadRequest("'from' must not be after 'to'.");

        var policies = await dbContext.MlPolicies
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .ToListAsync(cancellationToken);

        // Skip policies that already have a run in flight so the call is idempotent and safe to
        // retry — it won't stack duplicate concurrent runs on the same policy.
        var pendingId = (int)MlTrainingRunStatus.Pending;
        var runningId = (int)MlTrainingRunStatus.Running;
        var inFlight = (await dbContext.MlTrainingRuns
                .Where(r => r.StatusId == pendingId || r.StatusId == runningId)
                .Select(r => r.MlPolicyId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var results = new List<MlRetrainPolicyResult>(policies.Count);
        foreach (var policy in policies)
        {
            if (inFlight.Contains(policy.Id))
            {
                results.Add(new MlRetrainPolicyResult(
                    policy.Id, null, "Skipped",
                    $"Policy {policy.Id} already has a training run in progress."));
                continue;
            }

            var started = await StartTrainingAsync(policy, request.From, request.To, cancellationToken);
            results.Add(new MlRetrainPolicyResult(
                policy.Id,
                started.TrainingRunId,
                started.Status == MlTrainingRunStatus.Failed ? "Failed" : "Started",
                started.Message));
        }

        return Ok(results);
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Records a training run for the policy and forwards it to the ML service. On a forwarding
    /// failure the run is marked Failed and the returned status reflects that (the batch continues).
    /// </summary>
    private async Task<MlTrainStartedResponse> StartTrainingAsync(
        MlPolicy policy,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(toDate.Year, toDate.Month, toDate.Day, 23, 59, 0, TimeSpan.Zero);

        var run = new MlTrainingRun
        {
            MlPolicyId = policy.Id,
            From       = from,
            To         = to,
            StartedAt  = timeProvider.GetUtcNow(),
            StatusEnum = MlTrainingRunStatus.Pending
        };
        dbContext.MlTrainingRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        var forwarded = BuildTrainRequest(policy, run.Id, from, to);

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
            return new MlTrainStartedResponse(
                TrainingRunId: run.Id,
                Status:        run.StatusEnum,
                Message:       $"Training run {run.Id} (policy {policy.Id}) failed to start: {ex.Message}");
        }

        return new MlTrainStartedResponse(
            TrainingRunId: run.Id,
            Status:        run.StatusEnum,
            Message:       $"Training run {run.Id} (policy {policy.Id}) started on " +
                           $"{policy.Symbol.Code}/{policy.Interval.Code} " +
                           $"({fromDate:yyyy-MM-dd} -> {toDate:yyyy-MM-dd}).");
    }

    private static MlTrainRequest BuildTrainRequest(
        MlPolicy policy,
        long trainingRunId,
        DateTimeOffset from,
        DateTimeOffset to) =>
        new(
            MlPolicyId:    policy.Id,
            TrainingRunId: trainingRunId,
            Symbol:        policy.Symbol.Code,
            Interval:      policy.Interval.Code,
            FromDate:      from.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ToDate:        to.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ModelId:       policy.Id.ToString(),
            TotalTimesteps:                policy.TotalTimesteps,
            InitialBalance:                policy.InitialBalance,
            MaxCandlesPerTrade:            policy.MaxCandlesPerTrade,
            DailyProfitTarget:             policy.DailyProfit,
            DailyDrawdownLimit:            policy.DailyDrawdownLimit,
            SlippageRate:                  policy.Slippage,
            FeeRate:                       policy.Fee,
            RiskPerTrade:                  policy.RiskPerTrade,
            // Normalize defensively (older rows/blank values) so the sidecar always gets a valid scheme.
            ValidationScheme:              ValidationSchemes.Normalize(policy.ValidationScheme));

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
            FinalBalanceOos: r.FinalBalanceOos,
            PnlPctOos:      r.PnlPctOos,
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
