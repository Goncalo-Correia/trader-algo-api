using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>
/// Raw row from the ML service's <c>GET /models</c> registry (one per served policy). Snake_case to
/// match the Python payload; only the fields the .NET API needs are mapped (unknown fields are
/// ignored). This is an internal shape — the public API composes <see cref="MlServedModelResponse"/>.
/// </summary>
public sealed record MlModelInfoResponse(
    [property: JsonPropertyName("ml_policy_id")]      long? MlPolicyId,
    [property: JsonPropertyName("training_run_id")]   long? TrainingRunId,
    [property: JsonPropertyName("model_id")]          string? ModelId,
    [property: JsonPropertyName("final_balance")]     decimal? FinalBalance,
    [property: JsonPropertyName("pnl_pct")]           decimal? PnlPct,
    [property: JsonPropertyName("oos_pnl_pct")]       decimal? OosPnlPct,
    [property: JsonPropertyName("oos_final_balance")] decimal? OosFinalBalance,
    [property: JsonPropertyName("n_trades")]          int? NTrades,
    [property: JsonPropertyName("promoted")]          bool? Promoted,
    [property: JsonPropertyName("calibrated")]        bool? Calibrated,
    [property: JsonPropertyName("obs_dim")]           int? ObsDim,
    [property: JsonPropertyName("schema_version")]    int? SchemaVersion,
    [property: JsonPropertyName("run_id")]            string? RunId);

/// <summary>
/// Envelope the ML service wraps the model list in: <c>{ "models": [ ... ] }</c>.
/// </summary>
public sealed record MlModelsEnvelope(
    [property: JsonPropertyName("models")] IReadOnlyList<MlModelInfoResponse> Models);

/// <summary>
/// Public, per-policy view of the currently-served model, composed by joining the ML registry with
/// our policies. There is exactly one row per policy: <see cref="Served"/> is false when no model has
/// been promoted yet (or the policy is mid-retrain). camelCase like the rest of the policy/run API.
/// </summary>
public sealed record MlServedModelResponse(
    [property: JsonPropertyName("mlPolicyId")]          long MlPolicyId,
    [property: JsonPropertyName("symbolCode")]          string SymbolCode,
    [property: JsonPropertyName("intervalCode")]        string IntervalCode,
    [property: JsonPropertyName("served")]              bool Served,
    [property: JsonPropertyName("servedTrainingRunId")] long? ServedTrainingRunId,
    [property: JsonPropertyName("modelId")]             string? ModelId,
    [property: JsonPropertyName("finalBalance")]        decimal? FinalBalance,
    [property: JsonPropertyName("pnlPct")]              decimal? PnlPct,
    [property: JsonPropertyName("oosFinalBalance")]     decimal? OosFinalBalance,
    [property: JsonPropertyName("oosPnlPct")]           decimal? OosPnlPct,
    [property: JsonPropertyName("nTrades")]             int? NTrades,
    [property: JsonPropertyName("calibrated")]          bool? Calibrated,
    [property: JsonPropertyName("obsDim")]              int? ObsDim,
    [property: JsonPropertyName("schemaVersion")]       int? SchemaVersion,
    [property: JsonPropertyName("runId")]               string? RunId);
