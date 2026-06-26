using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>
/// Describes the model currently served for a policy, as returned by the ML service's
/// <c>GET /models</c> endpoint. Use this — not "a training run completed" — to know which model
/// is actually live (promotion is gated and risk-aware; a Completed run may not be promoted).
/// Unknown JSON fields are ignored, so this stays compatible with additive ML changes.
/// </summary>
public sealed record MlModelInfoResponse(
    [property: JsonPropertyName("ml_policy_id")]      long? MlPolicyId,
    [property: JsonPropertyName("model_id")]          string? ModelId,
    [property: JsonPropertyName("oos_pnl_pct")]       decimal? OosPnlPct,
    [property: JsonPropertyName("oos_final_balance")] decimal? OosFinalBalance,
    [property: JsonPropertyName("calibrated")]        bool? Calibrated,
    [property: JsonPropertyName("obs_dim")]           int? ObsDim,
    [property: JsonPropertyName("schema_version")]    int? SchemaVersion);
