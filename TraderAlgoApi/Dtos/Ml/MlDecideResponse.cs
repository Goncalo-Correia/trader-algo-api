using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlDecideResponse(
    [property: JsonPropertyName("action")]      int Action,
    [property: JsonPropertyName("action_name")] string ActionName,
    [property: JsonPropertyName("confidence")]  double Confidence,
    [property: JsonPropertyName("model_id")]    string ModelId,
    [property: JsonPropertyName("ml_policy_id")] long MlPolicyId);
