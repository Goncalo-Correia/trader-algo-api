using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Binance;

public sealed record BinanceKlineStreamDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("interval")] string Interval,
    [property: JsonPropertyName("eventTime")] long EventTime,
    [property: JsonPropertyName("time")] long Time,
    [property: JsonPropertyName("open")] decimal Open,
    [property: JsonPropertyName("high")] decimal High,
    [property: JsonPropertyName("low")] decimal Low,
    [property: JsonPropertyName("close")] decimal Close,
    [property: JsonPropertyName("volume")] decimal Volume,
    [property: JsonPropertyName("isClosed")] bool IsClosed);
