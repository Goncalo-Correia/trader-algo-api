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
    // In ATR-regime mode (policy trained with risk_per_trade) the sidecar returns SlBracket/TpBracket
    // null, SlAtrMult = risk_per_trade / ATR-at-entry (so SlAtrMult × ATR reproduces the fixed stop),
    // and TpRMult the regime take-profit multiple; the formulas below are unchanged either way.
    [property: JsonPropertyName("sl_bracket")]  int? SlBracket = null,
    [property: JsonPropertyName("tp_bracket")]  int? TpBracket = null,
    [property: JsonPropertyName("sl_atr_mult")] decimal? SlAtrMult = null,
    [property: JsonPropertyName("tp_r_mult")]   decimal? TpRMult = null,
    // Regime-selected order size. Non-null (e.g. 1.0 / 0.5 / 0.25) when the served model was trained
    // with risk_per_trade (ATR-regime mode) — apply it verbatim, do NOT re-derive size from
    // risk_per_trade / stop. Null in legacy menu mode, where the engine keeps its own sizing.
    [property: JsonPropertyName("quantity")]    decimal? Quantity = null);
