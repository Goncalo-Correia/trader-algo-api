using System.Text.Json.Serialization;

namespace TraderAlgoApi.Services.MarketData.Alpaca;

/// <summary>Single bar from the Alpaca historical bars REST response.</summary>
internal sealed record AlpacaBar(
    [property: JsonPropertyName("t")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("o")] decimal        Open,
    [property: JsonPropertyName("h")] decimal        High,
    [property: JsonPropertyName("l")] decimal        Low,
    [property: JsonPropertyName("c")] decimal        Close,
    [property: JsonPropertyName("v")] long            Volume,
    [property: JsonPropertyName("n")] int            NumberOfTrades,
    [property: JsonPropertyName("vw")] decimal       Vwap);
