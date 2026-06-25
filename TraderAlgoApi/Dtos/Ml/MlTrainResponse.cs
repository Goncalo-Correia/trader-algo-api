using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlTrainResponse(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("ml_policy_id")] long MlPolicyId,
    [property: JsonPropertyName("training_run_id")] long TrainingRunId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message);
