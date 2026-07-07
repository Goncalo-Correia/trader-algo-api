using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>
/// Read models for the performance-telemetry API. These mirror the Pydantic response models
/// exposed by the Python ML sidecar (see trader-algo-ml app/schemas/api.py), reading the
/// denormalised <c>training_*</c> tables the sidecar writes to the shared Postgres.
/// </summary>

/// <summary>Run summary: status, scheme, promotion/gate result, headline in-sample vs OOS.</summary>
public sealed record RunPerformanceResponse(
    [property: JsonPropertyName("runId")]                 string? RunId,
    [property: JsonPropertyName("mlPolicyId")]            int? MlPolicyId,
    [property: JsonPropertyName("scheme")]                string? Scheme,
    [property: JsonPropertyName("fromDate")]              string? FromDate,
    [property: JsonPropertyName("toDate")]                string? ToDate,
    [property: JsonPropertyName("status")]                string? Status,
    [property: JsonPropertyName("promoted")]              bool? Promoted,
    [property: JsonPropertyName("gatePassed")]            bool? GatePassed,
    [property: JsonPropertyName("gateDetail")]            string? GateDetail,
    [property: JsonPropertyName("seed")]                  int? Seed,
    [property: JsonPropertyName("obsDim")]                int? ObsDim,
    [property: JsonPropertyName("schemaVersion")]         int? SchemaVersion,
    [property: JsonPropertyName("inSamplePnlPct")]        double? InSamplePnlPct,
    [property: JsonPropertyName("oosPnlPct")]             double? OosPnlPct,
    [property: JsonPropertyName("oosSharpe")]             double? OosSharpe,
    [property: JsonPropertyName("oosProfitFactor")]       double? OosProfitFactor,
    [property: JsonPropertyName("oosMaxDdPct")]           double? OosMaxDdPct,
    [property: JsonPropertyName("inSampleMinusOosPnlPct")] double? InSampleMinusOosPnlPct,
    [property: JsonPropertyName("nFolds")]                int? NFolds,
    [property: JsonPropertyName("createdAt")]             DateTimeOffset? CreatedAt);

/// <summary>Reward-vs-timesteps point (mean/std episode reward, episode length).</summary>
public sealed record LearningCurvePointResponse(
    [property: JsonPropertyName("timesteps")]     int? Timesteps,
    [property: JsonPropertyName("meanEpReward")]  double? MeanEpReward,
    [property: JsonPropertyName("stdEpReward")]   double? StdEpReward,
    [property: JsonPropertyName("meanEpLength")]  double? MeanEpLength);

/// <summary>Per-checkpoint train/val reward + drawdown, q_train/q_val/score, eligible, is_best.</summary>
public sealed record CheckpointEvalResponse(
    [property: JsonPropertyName("timesteps")]   int? Timesteps,
    [property: JsonPropertyName("trainEvalR")]  double? TrainEvalR,
    [property: JsonPropertyName("valR")]        double? ValR,
    [property: JsonPropertyName("trainDdPct")]  double? TrainDdPct,
    [property: JsonPropertyName("valDdPct")]    double? ValDdPct,
    [property: JsonPropertyName("qTrain")]      double? QTrain,
    [property: JsonPropertyName("qVal")]        double? QVal,
    [property: JsonPropertyName("score")]       double? Score,
    [property: JsonPropertyName("gap")]         double? Gap,
    [property: JsonPropertyName("eligible")]    bool? Eligible,
    [property: JsonPropertyName("isBest")]      bool? IsBest);

/// <summary>Per-fold walk-forward result (block val_* or sliding test_*).</summary>
public sealed record FoldResultResponse(
    [property: JsonPropertyName("fold")]          int? Fold,
    [property: JsonPropertyName("scheme")]        string? Scheme,
    [property: JsonPropertyName("isOos")]         bool? IsOos,
    [property: JsonPropertyName("trainStart")]    string? TrainStart,
    [property: JsonPropertyName("trainEnd")]      string? TrainEnd,
    [property: JsonPropertyName("valStart")]      string? ValStart,
    [property: JsonPropertyName("valEnd")]        string? ValEnd,
    [property: JsonPropertyName("testStart")]     string? TestStart,
    [property: JsonPropertyName("testEnd")]       string? TestEnd,
    [property: JsonPropertyName("returnPct")]     double? ReturnPct,
    [property: JsonPropertyName("sharpe")]        double? Sharpe,
    [property: JsonPropertyName("profitFactor")]  double? ProfitFactor,
    [property: JsonPropertyName("winRatePct")]    double? WinRatePct,
    [property: JsonPropertyName("maxDdPct")]      double? MaxDdPct,
    [property: JsonPropertyName("avgR")]          double? AvgR,
    [property: JsonPropertyName("nTrades")]       int? NTrades);

/// <summary>full_report metrics for a split (train/val/test/oos).</summary>
public sealed record SplitMetricsResponse(
    [property: JsonPropertyName("split")]                string? Split,
    [property: JsonPropertyName("totalReturnPct")]       double? TotalReturnPct,
    [property: JsonPropertyName("annualizedReturnPct")]  double? AnnualizedReturnPct,
    [property: JsonPropertyName("maxDrawdownPct")]       double? MaxDrawdownPct,
    [property: JsonPropertyName("sharpeLike")]           double? SharpeLike,
    [property: JsonPropertyName("sortinoRatio")]         double? SortinoRatio,
    [property: JsonPropertyName("calmarRatio")]          double? CalmarRatio,
    [property: JsonPropertyName("profitFactor")]         double? ProfitFactor,
    [property: JsonPropertyName("winRatePct")]           double? WinRatePct,
    [property: JsonPropertyName("avgR")]                 double? AvgR,
    [property: JsonPropertyName("nTrades")]              int? NTrades);

/// <summary>One equity/drawdown sample of a split's equity curve.</summary>
public sealed record EquityPointResponse(
    [property: JsonPropertyName("split")]        string? Split,
    [property: JsonPropertyName("ts")]           long? Ts,
    [property: JsonPropertyName("equity")]       double? Equity,
    [property: JsonPropertyName("drawdownPct")]  double? DrawdownPct,
    [property: JsonPropertyName("realizedPnl")]  double? RealizedPnl,
    [property: JsonPropertyName("position")]     int? Position);

/// <summary>Paginated equity/drawdown series for one split (or the stitched OOS curve).</summary>
public sealed record EquitySeriesResponse(
    [property: JsonPropertyName("runId")]  long RunId,
    [property: JsonPropertyName("split")]  string Split,
    [property: JsonPropertyName("total")]  int Total,
    [property: JsonPropertyName("limit")]  int Limit,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("points")] IReadOnlyList<EquityPointResponse> Points);

/// <summary>One telemetry trade row (drives R-dist / monthly / exit / bracket charts).</summary>
public sealed record TradeRecordResponse(
    [property: JsonPropertyName("split")]        string? Split,
    [property: JsonPropertyName("entryTime")]    long? EntryTime,
    [property: JsonPropertyName("exitTime")]     long? ExitTime,
    [property: JsonPropertyName("direction")]    string? Direction,
    [property: JsonPropertyName("entryPrice")]   double? EntryPrice,
    [property: JsonPropertyName("exitPrice")]    double? ExitPrice,
    [property: JsonPropertyName("sl")]           double? Sl,
    [property: JsonPropertyName("tp")]           double? Tp,
    [property: JsonPropertyName("slAtrMult")]    double? SlAtrMult,
    [property: JsonPropertyName("tpRBracket")]   double? TpRBracket,
    [property: JsonPropertyName("units")]        double? Units,
    [property: JsonPropertyName("pnl")]          double? Pnl,
    [property: JsonPropertyName("rMult")]        double? RMult,
    [property: JsonPropertyName("barsInTrade")]  int? BarsInTrade,
    [property: JsonPropertyName("exitReason")]   string? ExitReason);

/// <summary>Paginated trade log for a run (optionally filtered to one split).</summary>
public sealed record TradesResponse(
    [property: JsonPropertyName("runId")]  long RunId,
    [property: JsonPropertyName("split")]  string? Split,
    [property: JsonPropertyName("total")]  int Total,
    [property: JsonPropertyName("limit")]  int Limit,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("trades")] IReadOnlyList<TradeRecordResponse> Trades);

/// <summary>Per-feature stationarity, 1-bar Spearman signal, collinearity flags.</summary>
public sealed record FeatureQualityResponse(
    [property: JsonPropertyName("feature")]        string? Feature,
    [property: JsonPropertyName("mean")]           double? Mean,
    [property: JsonPropertyName("std")]            double? Std,
    [property: JsonPropertyName("skew")]           double? Skew,
    [property: JsonPropertyName("excessKurt")]     double? ExcessKurt,
    [property: JsonPropertyName("cv")]             double? Cv,
    [property: JsonPropertyName("spearmanR1Bar")]  double? SpearmanR1Bar,
    [property: JsonPropertyName("spearmanP1Bar")]  double? SpearmanP1Bar,
    [property: JsonPropertyName("signalP05")]      bool? SignalP05);

/// <summary>
/// Pre-rendered chart artifact. <c>SignedUrl</c> mirrors the Python API's short-lived
/// Supabase-Storage signed URL; it is null here because this API has no Storage integration.
/// </summary>
public sealed record ChartArtifactResponse(
    [property: JsonPropertyName("chartKey")]     string? ChartKey,
    [property: JsonPropertyName("kind")]         string? Kind,
    [property: JsonPropertyName("storagePath")]  string? StoragePath,
    [property: JsonPropertyName("contentType")]  string? ContentType,
    [property: JsonPropertyName("createdAt")]    DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("signedUrl")]    string? SignedUrl);
