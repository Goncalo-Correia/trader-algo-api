using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Controllers;

/// <summary>
/// Read-only performance-telemetry API. Mirrors the Python ML sidecar's Performance API
/// (trader-algo-ml app/main.py), reading the denormalised <c>training_*</c> tables the sidecar
/// writes to the shared Postgres. Routes are namespaced under <c>/api/ml</c> alongside the other
/// ML endpoints. The DB <c>run_id</c> column is text; the route takes an int and compares as string
/// (matching the Python API's behaviour).
/// </summary>
[ApiController]
[Route("api/ml")]
public sealed class MlPerformanceController(ApplicationDbContext dbContext) : ControllerBase
{
    // Page-size / list bounds so a caller can't request an unbounded slice (or pass negative
    // Take/Skip values that would fault the provider).
    private const int MaxPageSize = 10_000;
    private const int MaxPolicyRuns = 500;

    /// <summary>Run summary: status, scheme, promotion/gate result, headline in-sample vs OOS.</summary>
    [HttpGet("training-runs/{runId:long}/performance")]
    public async Task<ActionResult<RunPerformanceResponse>> GetRunPerformance(
        long runId,
        CancellationToken cancellationToken)
    {
        var key = runId.ToString();
        var row = await dbContext.TrainingRunPerformances
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RunId == key, cancellationToken);

        return row is null
            ? NotFound($"No performance telemetry for run {runId}.")
            : Ok(ToDto(row));
    }

    /// <summary>Reward-vs-timesteps points (mean/std episode reward, episode length).</summary>
    [HttpGet("training-runs/{runId:long}/learning-curve")]
    public async Task<ActionResult<IReadOnlyList<LearningCurvePointResponse>>> GetLearningCurve(
        long runId,
        CancellationToken cancellationToken)
    {
        var key = runId.ToString();
        var rows = await dbContext.TrainingLearningCurves
            .AsNoTracking()
            .Where(r => r.RunId == key)
            .OrderBy(r => r.Timesteps)
            .Select(r => new LearningCurvePointResponse(
                r.Timesteps, r.MeanEpReward, r.StdEpReward, r.MeanEpLength))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>Per-eval train/val reward + drawdown, q_train/q_val/score, eligible, is_best.</summary>
    [HttpGet("training-runs/{runId:long}/checkpoint-evals")]
    public async Task<ActionResult<IReadOnlyList<CheckpointEvalResponse>>> GetCheckpointEvals(
        long runId,
        CancellationToken cancellationToken)
    {
        var key = runId.ToString();
        var rows = await dbContext.TrainingCheckpointEvals
            .AsNoTracking()
            .Where(r => r.RunId == key)
            .OrderBy(r => r.Timesteps)
            .Select(r => new CheckpointEvalResponse(
                r.Timesteps, r.TrainEvalR, r.ValR, r.TrainDdPct, r.ValDdPct,
                r.QTrain, r.QVal, r.Score, r.Gap, r.Eligible, r.IsBest))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>Per-fold walk-forward results (block scheme; val_* window, optional held-out test_*) + aggregate.</summary>
    [HttpGet("training-runs/{runId:long}/folds")]
    public async Task<ActionResult<IReadOnlyList<FoldResultResponse>>> GetFolds(
        long runId,
        CancellationToken cancellationToken)
    {
        var key = runId.ToString();
        var rows = await dbContext.TrainingFoldResults
            .AsNoTracking()
            .Where(r => r.RunId == key)
            .OrderBy(r => r.Fold)
            .Select(r => new FoldResultResponse(
                r.Fold, r.Scheme, r.IsOos, r.TrainStart, r.TrainEnd, r.ValStart, r.ValEnd,
                r.TestStart, r.TestEnd, r.ReturnPct, r.Sharpe, r.ProfitFactor, r.WinRatePct,
                r.MaxDdPct, r.AvgR, r.NTrades))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>full_report metrics for a split (train/val/test/oos); omit split for all.</summary>
    [HttpGet("training-runs/{runId:long}/metrics")]
    public async Task<ActionResult<IReadOnlyList<SplitMetricsResponse>>> GetMetrics(
        long runId,
        [FromQuery] string? split,
        CancellationToken cancellationToken)
    {
        var key = runId.ToString();
        var query = dbContext.TrainingSplitMetrics
            .AsNoTracking()
            .Where(r => r.RunId == key);

        if (!string.IsNullOrWhiteSpace(split))
            query = query.Where(r => r.Split == split);

        var rows = await query
            .Select(r => new SplitMetricsResponse(
                r.Split, r.TotalReturnPct, r.AnnualizedReturnPct, r.MaxDrawdownPct, r.SharpeLike,
                r.SortinoRatio, r.CalmarRatio, r.ProfitFactor, r.WinRatePct, r.AvgR, r.NTrades))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>
    /// Equity + drawdown series for a split, or the stitched compounded OOS curve (stitched=true).
    /// Paginated by (limit, offset); <c>total</c> is the full count for that (run, split).
    /// </summary>
    [HttpGet("training-runs/{runId:long}/equity")]
    public async Task<ActionResult<EquitySeriesResponse>> GetEquity(
        long runId,
        [FromQuery] string split = "oos",
        [FromQuery] bool stitched = false,
        [FromQuery] int limit = 5000,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, MaxPageSize);
        offset = Math.Max(offset, 0);

        var key = runId.ToString();
        var resolved = stitched ? "stitched" : split;

        var baseQuery = dbContext.TrainingEquityPoints
            .AsNoTracking()
            .Where(r => r.RunId == key && r.Split == resolved);

        var total = await baseQuery.CountAsync(cancellationToken);

        var points = await baseQuery
            .OrderBy(r => r.Ts)
            .Skip(offset)
            .Take(limit)
            .Select(r => new EquityPointResponse(
                r.Split, r.Ts, r.Equity, r.DrawdownPct, r.RealizedPnl, r.Position))
            .ToListAsync(cancellationToken);

        return Ok(new EquitySeriesResponse(runId, resolved, total, limit, offset, points));
    }

    /// <summary>Trade log (drives the R-dist / monthly / exit / entry / bracket charts). Paginated.</summary>
    [HttpGet("training-runs/{runId:long}/trades")]
    public async Task<ActionResult<TradesResponse>> GetTrades(
        long runId,
        [FromQuery] string? split,
        [FromQuery] int limit = 5000,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, MaxPageSize);
        offset = Math.Max(offset, 0);

        var key = runId.ToString();
        var baseQuery = dbContext.TrainingTrades
            .AsNoTracking()
            .Where(r => r.RunId == key);

        if (!string.IsNullOrWhiteSpace(split))
            baseQuery = baseQuery.Where(r => r.Split == split);

        var total = await baseQuery.CountAsync(cancellationToken);

        var trades = await baseQuery
            .OrderBy(r => r.EntryTime)
            .Skip(offset)
            .Take(limit)
            .Select(r => new TradeRecordResponse(
                r.Split, r.EntryTime, r.ExitTime, r.Direction, r.EntryPrice, r.ExitPrice,
                r.Sl, r.Tp, r.SlAtrMult, r.TpRBracket, r.Units, r.Pnl, r.RMult,
                r.BarsInTrade, r.ExitReason))
            .ToListAsync(cancellationToken);

        return Ok(new TradesResponse(runId, split, total, limit, offset, trades));
    }

    /// <summary>Per-feature stationarity, 1-bar Spearman signal, collinearity flags.</summary>
    [HttpGet("training-runs/{runId:long}/feature-quality")]
    public async Task<ActionResult<IReadOnlyList<FeatureQualityResponse>>> GetFeatureQuality(
        long runId,
        CancellationToken cancellationToken)
    {
        var key = runId.ToString();
        var rows = await dbContext.TrainingFeatureQualities
            .AsNoTracking()
            .Where(r => r.RunId == key)
            .OrderBy(r => r.Feature)
            .Select(r => new FeatureQualityResponse(
                r.Feature, r.Mean, r.Std, r.Skew, r.ExcessKurt, r.Cv,
                r.SpearmanR1Bar, r.SpearmanP1Bar, r.SignalP05))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>
    /// Pre-rendered chart artifacts. The Python API returns a short-lived Supabase-Storage signed
    /// URL per row; this API has no Storage integration, so <c>signedUrl</c> is always null and
    /// callers must resolve <c>storagePath</c> themselves.
    /// </summary>
    [HttpGet("training-runs/{runId:long}/charts")]
    public async Task<ActionResult<IReadOnlyList<ChartArtifactResponse>>> GetCharts(
        long runId,
        CancellationToken cancellationToken)
    {
        var key = runId.ToString();
        var rows = await dbContext.TrainingChartArtifacts
            .AsNoTracking()
            .Where(r => r.RunId == key)
            .Select(r => new ChartArtifactResponse(
                r.ChartKey, r.Kind, r.StoragePath, r.ContentType, r.CreatedAt, null))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>Chronological run list with headline metrics — the run-over-run trend view.</summary>
    [HttpGet("policies/{policyId:int}/runs")]
    public async Task<ActionResult<IReadOnlyList<RunPerformanceResponse>>> GetPolicyRuns(
        int policyId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.TrainingRunPerformances
            .AsNoTracking()
            .Where(r => r.MlPolicyId == policyId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(MaxPolicyRuns)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>The currently-promoted run's performance summary for a policy.</summary>
    [HttpGet("policies/{policyId:int}/performance")]
    public async Task<ActionResult<RunPerformanceResponse>> GetPolicyPerformance(
        int policyId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.TrainingRunPerformances
            .AsNoTracking()
            .Where(r => r.MlPolicyId == policyId && r.Promoted == true)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? NotFound($"No promoted run telemetry for policy {policyId}.")
            : Ok(ToDto(row));
    }

    // -------------------------------------------------------------------------

    private static RunPerformanceResponse ToDto(Models.Telemetry.TrainingRunPerformance r) =>
        new(
            RunId:                  r.RunId,
            MlPolicyId:             r.MlPolicyId,
            Scheme:                 r.Scheme,
            FromDate:               r.FromDate,
            ToDate:                 r.ToDate,
            Status:                 r.Status,
            Promoted:               r.Promoted,
            GatePassed:             r.GatePassed,
            GateDetail:             r.GateDetail,
            Seed:                   r.Seed,
            ObsDim:                 r.ObsDim,
            SchemaVersion:          r.SchemaVersion,
            InSamplePnlPct:         r.InSamplePnlPct,
            OosPnlPct:              r.OosPnlPct,
            OosSharpe:              r.OosSharpe,
            OosProfitFactor:        r.OosProfitFactor,
            OosMaxDdPct:            r.OosMaxDdPct,
            InSampleMinusOosPnlPct: r.InSampleMinusOosPnlPct,
            NFolds:                 r.NFolds,
            CreatedAt:              r.CreatedAt);
}
