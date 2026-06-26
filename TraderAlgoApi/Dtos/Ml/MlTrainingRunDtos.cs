using System.Text.Json.Serialization;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>
/// Starts a training run for an existing policy over a date range. Only dates are supplied;
/// the controller normalises <c>From</c> to 00:00 and <c>To</c> to 23:59 of the given day.
/// </summary>
public sealed record MlStartTrainingRequest(
    [property: JsonPropertyName("mlPolicyId")] long MlPolicyId,
    [property: JsonPropertyName("from")]       DateOnly From,
    [property: JsonPropertyName("to")]         DateOnly To);

/// <summary>
/// Starts a training run for every policy over a shared date range. Used for the one-time,
/// post-upgrade retrain when the ML observation schema changes.
/// </summary>
public sealed record MlRetrainAllRequest(
    [property: JsonPropertyName("from")] DateOnly From,
    [property: JsonPropertyName("to")]   DateOnly To);

/// <summary>Returned to the client when a training run is kicked off.</summary>
public sealed record MlTrainStartedResponse(
    [property: JsonPropertyName("trainingRunId")] long TrainingRunId,
    [property: JsonPropertyName("status")]        MlTrainingRunStatus Status,
    [property: JsonPropertyName("message")]       string Message);

/// <summary>Read model for a persisted training run (model/symbol/interval come from its policy).</summary>
public sealed record MlTrainingRunResponse(
    [property: JsonPropertyName("id")]             long Id,
    [property: JsonPropertyName("mlPolicyId")]     long MlPolicyId,
    [property: JsonPropertyName("symbolCode")]     string SymbolCode,
    [property: JsonPropertyName("intervalCode")]   string IntervalCode,
    [property: JsonPropertyName("from")]           long From,
    [property: JsonPropertyName("to")]             long To,
    [property: JsonPropertyName("startedAt")]      long StartedAt,
    [property: JsonPropertyName("completedAt")]    long? CompletedAt,
    [property: JsonPropertyName("status")]         MlTrainingRunStatus Status,
    [property: JsonPropertyName("totalTimesteps")] int? TotalTimesteps,
    [property: JsonPropertyName("finalBalance")]   decimal? FinalBalance,
    [property: JsonPropertyName("pnlPct")]         decimal? PnlPct,
    [property: JsonPropertyName("finalBalanceOos")] decimal? FinalBalanceOos,
    [property: JsonPropertyName("pnlPctOos")]       decimal? PnlPctOos,
    [property: JsonPropertyName("nTrades")]        int? NTrades,
    [property: JsonPropertyName("runId")]          string? RunId,
    [property: JsonPropertyName("tracking")]       MlflowTrainingTrackingSummaryDto? Tracking = null);

/// <summary>
/// Webhook body the Python ML service PATCHes when a training run finishes (or fails).
/// Uses snake_case to match the Python payload.
/// </summary>
public sealed record MlTrainingRunCompleteRequest(
    [property: JsonPropertyName("status")]        string Status,
    [property: JsonPropertyName("final_balance")] decimal? FinalBalance = null,
    [property: JsonPropertyName("pnl_pct")]       decimal? PnlPct = null,
    [property: JsonPropertyName("final_balance_oos")] decimal? FinalBalanceOos = null,
    [property: JsonPropertyName("pnl_pct_oos")]   decimal? PnlPctOos = null,
    [property: JsonPropertyName("n_trades")]      int? NTrades = null,
    [property: JsonPropertyName("run_id")]        string? RunId = null);
