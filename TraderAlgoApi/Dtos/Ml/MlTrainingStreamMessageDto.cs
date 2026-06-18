using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>
/// Envelope streamed to the client over the training-replay WebSocket. Mirrors the
/// backtest stream's { type, data } shape so the front-end can reuse its renderer.
/// </summary>
public sealed record MlTrainingStreamMessageDto<T>(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] T Data);

/// <summary>One model decision aligned to a candle, emitted alongside the candle it applies to.</summary>
public sealed record MlDecisionDto(
    [property: JsonPropertyName("time")]       long Time,
    [property: JsonPropertyName("action")]     int Action,
    [property: JsonPropertyName("actionName")] string ActionName,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("probs")]      IReadOnlyList<double> Probs,
    [property: JsonPropertyName("position")]   int Position,
    [property: JsonPropertyName("balance")]    decimal Balance);
