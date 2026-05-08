using System.Text.Json.Serialization;

namespace TraderAlgoApi.Services.MarketData.Alpaca;

internal sealed record AlpacaBarsResponse(
    [property: JsonPropertyName("bars")]            IReadOnlyList<AlpacaBar> Bars,
    [property: JsonPropertyName("next_page_token")] string?                  NextPageToken);
