using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Session;

public sealed record VolumeProfileLevelDto(
    [property: JsonPropertyName("priceFrom")] decimal PriceFrom,
    [property: JsonPropertyName("priceTo")]   decimal PriceTo,
    [property: JsonPropertyName("volume")]    decimal Volume,
    [property: JsonPropertyName("buyVolume")] decimal BuyVolume);
