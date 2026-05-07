using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.TradingAccounts;

public sealed record TradingAccountResponseDto(
    [property: JsonPropertyName("id")]               long    Id,
    [property: JsonPropertyName("name")]             string  Name,
    [property: JsonPropertyName("initialBalance")]   decimal InitialBalance,
    [property: JsonPropertyName("currentBalance")]   decimal CurrentBalance,
    [property: JsonPropertyName("isActive")]         bool    IsActive,
    [property: JsonPropertyName("createdAt")]        long    CreatedAt);
