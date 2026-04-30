using System.Text.Json.Serialization;
using TraderAlgoApi.Dtos.Trades;

namespace TraderAlgoApi.Dtos.TradeEvents;

public sealed record TradeEventDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("tradingAccountId")] long? TradingAccountId,
    [property: JsonPropertyName("tradeId")] long? TradeId,
    [property: JsonPropertyName("symbolCode")] string? SymbolCode,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("createdAt")] long CreatedAt,
    [property: JsonPropertyName("trade")] TradeResponseDto? Trade);
