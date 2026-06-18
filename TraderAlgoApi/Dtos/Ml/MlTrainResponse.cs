using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlTrainResponse(
    [property: JsonPropertyName("run_id")]   string RunId,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("status")]   string Status,
    [property: JsonPropertyName("message")]  string Message);
