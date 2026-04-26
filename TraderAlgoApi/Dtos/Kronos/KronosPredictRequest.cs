using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Kronos;

public sealed record KronosPredictRequest(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("candles")] IReadOnlyList<KronosCandleDto> Candles,
    [property: JsonPropertyName("pred_len")] int PredLen,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("top_k")] int TopK,
    [property: JsonPropertyName("top_p")] double TopP,
    [property: JsonPropertyName("sample_count")] int SampleCount);
