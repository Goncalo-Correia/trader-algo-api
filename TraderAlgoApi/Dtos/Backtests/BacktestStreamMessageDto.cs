using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Backtests;

public sealed record BacktestStreamMessageDto<T>(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] T Data);

public sealed record TradeBracketUpdateDto(
    [property: JsonPropertyName("tradeId")]     long TradeId,
    [property: JsonPropertyName("stopLoss")]    decimal? StopLoss,
    [property: JsonPropertyName("takeProfit")]  decimal? TakeProfit);
