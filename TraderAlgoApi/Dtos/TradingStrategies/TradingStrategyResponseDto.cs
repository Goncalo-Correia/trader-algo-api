using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.TradingStrategies;

public sealed record TradingStrategyResponseDto(
    [property: JsonPropertyName("id")]   int    Id,
    [property: JsonPropertyName("name")] string Name);
