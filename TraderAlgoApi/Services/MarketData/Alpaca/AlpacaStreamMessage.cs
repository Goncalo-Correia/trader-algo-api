using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraderAlgoApi.Services.MarketData.Alpaca;

/// <summary>Base discriminated-union envelope for all Alpaca WebSocket messages.</summary>
internal abstract record AlpacaStreamMessage(
    [property: JsonPropertyName("T")] string Type);

/// <summary>Minute bar (T = "b").</summary>
internal sealed record AlpacaMinuteBarMessage(
    [property: JsonPropertyName("S")]  string         Symbol,
    [property: JsonPropertyName("t")]  DateTimeOffset Timestamp,
    [property: JsonPropertyName("o")]  decimal        Open,
    [property: JsonPropertyName("h")]  decimal        High,
    [property: JsonPropertyName("l")]  decimal        Low,
    [property: JsonPropertyName("c")]  decimal        Close,
    [property: JsonPropertyName("v")]  long            Volume,
    [property: JsonPropertyName("n")]  int            NumberOfTrades,
    [property: JsonPropertyName("vw")] decimal        Vwap) : AlpacaStreamMessage("b");

/// <summary>Daily bar (T = "d").</summary>
internal sealed record AlpacaDailyBarMessage(
    [property: JsonPropertyName("S")]  string         Symbol,
    [property: JsonPropertyName("t")]  DateTimeOffset Timestamp,
    [property: JsonPropertyName("o")]  decimal        Open,
    [property: JsonPropertyName("h")]  decimal        High,
    [property: JsonPropertyName("l")]  decimal        Low,
    [property: JsonPropertyName("c")]  decimal        Close,
    [property: JsonPropertyName("v")]  long            Volume,
    [property: JsonPropertyName("n")]  int            NumberOfTrades,
    [property: JsonPropertyName("vw")] decimal        Vwap) : AlpacaStreamMessage("d");

/// <summary>Individual trade tick (T = "t"), used for PriceFeed.</summary>
internal sealed record AlpacaTradeMessage(
    [property: JsonPropertyName("S")] string         Symbol,
    [property: JsonPropertyName("p")] decimal        Price,
    [property: JsonPropertyName("t")] DateTimeOffset Timestamp) : AlpacaStreamMessage("t");

internal static class AlpacaStreamParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Parses a raw Alpaca WebSocket frame (which is always a JSON array) and
    /// returns the typed messages it contains.
    /// </summary>
    internal static IEnumerable<AlpacaStreamMessage> Parse(byte[] bytes)
    {
        using var doc  = JsonDocument.Parse(bytes);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var element in root.EnumerateArray())
        {
            if (!element.TryGetProperty("T", out var typeProp))
                continue;

            var type = typeProp.GetString();

            if (type == "b")
            {
                var msg = element.Deserialize<AlpacaMinuteBarMessage>(Options);
                if (msg is not null) yield return msg;
                continue;
            }

            if (type == "d")
            {
                var msg = element.Deserialize<AlpacaDailyBarMessage>(Options);
                if (msg is not null) yield return msg;
                continue;
            }

            if (type == "t")
            {
                var msg = element.Deserialize<AlpacaTradeMessage>(Options);
                if (msg is not null) yield return msg;
            }
            // Unknown / control messages ("connected", "success", "subscription") are ignored.
        }
    }
}
