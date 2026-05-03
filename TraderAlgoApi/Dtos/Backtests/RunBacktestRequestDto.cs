using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Backtests;

public sealed record RunBacktestRequestDto(
    [property: JsonPropertyName("symbol")]         string SymbolCode,
    [property: JsonPropertyName("interval")]       string IntervalCode,
    [property: JsonPropertyName("strategyId")]     int TradingStrategyId,
    [property: JsonPropertyName("from")]           DateTimeOffset From,
    [property: JsonPropertyName("to")]             DateTimeOffset To,
    [property: JsonPropertyName("quantity")]       decimal Quantity,
    [property: JsonPropertyName("stopLoss")]       decimal? StopLoss,
    [property: JsonPropertyName("takeProfit")]     decimal? TakeProfit,
    [property: JsonPropertyName("initialBalance")] decimal InitialBalance);
