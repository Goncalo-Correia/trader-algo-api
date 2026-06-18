using System.Text.Json.Serialization;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>Returned to the client when a training run is kicked off.</summary>
public sealed record MlTrainStartedResponse(
    [property: JsonPropertyName("trainingRunId")] long TrainingRunId,
    [property: JsonPropertyName("modelId")]       string ModelId,
    [property: JsonPropertyName("status")]        MlTrainingRunStatus Status,
    [property: JsonPropertyName("message")]       string Message);

/// <summary>Read model for a persisted training run.</summary>
public sealed record MlTrainingRunResponse(
    [property: JsonPropertyName("id")]             long Id,
    [property: JsonPropertyName("modelId")]        string ModelId,
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
    [property: JsonPropertyName("nTrades")]        int? NTrades,
    [property: JsonPropertyName("runId")]          string? RunId);

/// <summary>
/// Webhook body the Python ML service PATCHes when a training run finishes (or fails).
/// Uses snake_case to match the Python payload.
/// </summary>
public sealed record MlTrainingRunCompleteRequest(
    [property: JsonPropertyName("status")]        string Status,
    [property: JsonPropertyName("final_balance")] decimal? FinalBalance = null,
    [property: JsonPropertyName("pnl_pct")]       decimal? PnlPct = null,
    [property: JsonPropertyName("n_trades")]      int? NTrades = null,
    [property: JsonPropertyName("run_id")]        string? RunId = null);
