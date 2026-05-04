using System.Text.Json.Serialization;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Dtos.TradeBots;

public sealed record TradeBotResponseDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("tradingAccountId")] long? TradingAccountId,
    [property: JsonPropertyName("tradingAccountName")] string? TradingAccountName,
    [property: JsonPropertyName("backtestId")] long? BacktestId,
    [property: JsonPropertyName("tradingStrategy")] TradingStrategy TradingStrategy,
    [property: JsonPropertyName("symbolCode")] string SymbolCode,
    [property: JsonPropertyName("intervalCode")] string IntervalCode,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("stopLoss")] decimal? StopLoss,
    [property: JsonPropertyName("takeProfit")] decimal? TakeProfit,
    [property: JsonPropertyName("createdAt")] long CreatedAt,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt,
    [property: JsonPropertyName("lastSignalAt")] long? LastSignalAt);
