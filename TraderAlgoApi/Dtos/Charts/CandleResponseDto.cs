using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Charts;

public sealed record CandleResponseDto(
    [property: JsonPropertyName("time")] long Time,
    [property: JsonPropertyName("open")] decimal Open,
    [property: JsonPropertyName("high")] decimal High,
    [property: JsonPropertyName("low")] decimal Low,
    [property: JsonPropertyName("close")] decimal Close,
    [property: JsonPropertyName("volume")] decimal Volume,
    [property: JsonPropertyName("buyVolume")] decimal BuyVolume,
    [property: JsonPropertyName("sellVolume")] decimal SellVolume);
