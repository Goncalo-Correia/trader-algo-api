using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Backtests;

public sealed record CreateBacktestRequestDto(
    [property: JsonPropertyName("symbol")]         string SymbolCode,
    [property: JsonPropertyName("interval")]       string IntervalCode,
    [property: JsonPropertyName("from")]           DateTimeOffset From,
    [property: JsonPropertyName("to")]             DateTimeOffset To,
    [property: JsonPropertyName("initialBalance")] decimal InitialBalance,
    [property: JsonPropertyName("tradingStrategyId")] int? TradingStrategyId = null,
    [property: JsonPropertyName("quantity")]       decimal? Quantity = null,
    [property: JsonPropertyName("stopLoss")]       decimal? StopLoss = null,
    [property: JsonPropertyName("takeProfit")]     decimal? TakeProfit = null,
    [property: JsonPropertyName("breakeven")]       decimal? Breakeven = null,
    [property: JsonPropertyName("breakevenStop")]   decimal? BreakevenStop = null,
    [property: JsonPropertyName("fee")]             decimal Fee = 0,
    [property: JsonPropertyName("isNySessionOnly")] bool IsNySessionOnly = false,
    [property: JsonPropertyName("dailyProfitGoal")] decimal? DailyProfitGoal = null,
    [property: JsonPropertyName("maxLossesPerDay")]   int? MaxLossesPerDay = null,
    [property: JsonPropertyName("maxCandlesPerTrade")] int? MaxCandlesPerTrade = null);
