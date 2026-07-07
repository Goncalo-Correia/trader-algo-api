using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlDecideResponse(
    [property: JsonPropertyName("action")]      int Action,
    [property: JsonPropertyName("action_name")] string ActionName,
    [property: JsonPropertyName("confidence")]  double Confidence,
    [property: JsonPropertyName("model_id")]    string ModelId,
    [property: JsonPropertyName("ml_policy_id")] long MlPolicyId,
    // Chosen bracket at entry (§6). Indices into the model's SL ATR-multiplier / TP R-multiple
    // menus, plus the resolved multiplier values. All null on a Hold. SlAtrMult sizes the stop
    // (× ATR-at-entry); TpRMult sizes the take-profit (× stop distance).
    [property: JsonPropertyName("sl_bracket")]  int? SlBracket = null,
    [property: JsonPropertyName("tp_bracket")]  int? TpBracket = null,
    [property: JsonPropertyName("sl_atr_mult")] decimal? SlAtrMult = null,
    [property: JsonPropertyName("tp_r_mult")]   decimal? TpRMult = null);
