using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Kronos;

public sealed record KronosPredictResponse(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("predictions")] IReadOnlyList<KronosCandleDto> Predictions);
