using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Kronos;

public sealed record KronosCandleDto(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("open")] decimal Open,
    [property: JsonPropertyName("high")] decimal High,
    [property: JsonPropertyName("low")] decimal Low,
    [property: JsonPropertyName("close")] decimal Close,
    [property: JsonPropertyName("volume")] decimal Volume,
    [property: JsonPropertyName("amount")] decimal? Amount = null);
