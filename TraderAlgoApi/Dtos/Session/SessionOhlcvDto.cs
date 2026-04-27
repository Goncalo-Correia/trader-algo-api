using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Session;

public sealed record SessionOhlcvDto(
    [property: JsonPropertyName("open")]         decimal Open,
    [property: JsonPropertyName("high")]         decimal High,
    [property: JsonPropertyName("low")]          decimal Low,
    [property: JsonPropertyName("close")]        decimal Close,
    [property: JsonPropertyName("volume")]       decimal Volume,
    [property: JsonPropertyName("sessionStart")] long SessionStart,
    [property: JsonPropertyName("sessionEnd")]   long SessionEnd);
