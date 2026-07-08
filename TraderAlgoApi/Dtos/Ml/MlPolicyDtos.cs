using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>Create/update body for a training policy. Symbol/interval are codes; model is an id.</summary>
public sealed record MlPolicyRequest(
    [property: JsonPropertyName("symbol")]    string Symbol,
    [property: JsonPropertyName("interval")]  string Interval,
    [property: JsonPropertyName("totalTimesteps")]      int TotalTimesteps,
    [property: JsonPropertyName("initialBalance")]      decimal InitialBalance,
    [property: JsonPropertyName("fee")]                 decimal Fee,
    // slippage is an ATR fraction (offset = slippage × ATR-at-entry per fill), not a fixed offset.
    [property: JsonPropertyName("slippage")]            decimal Slippage,
    [property: JsonPropertyName("dailyProfit")]         decimal DailyProfit,
    [property: JsonPropertyName("dailyDrawdownLimit")]  decimal DailyDrawdownLimit,
    [property: JsonPropertyName("maxCandlesPerTrade")]  int MaxCandlesPerTrade,
    // Optional cash risked at the stop for volatility-targeted ML sizing (forwarded as risk_per_trade).
    [property: JsonPropertyName("riskPerTrade")]        decimal? RiskPerTrade = null);

public sealed record MlPolicyResponse(
    [property: JsonPropertyName("id")]            long Id,
    [property: JsonPropertyName("symbolId")]      int SymbolId,
    [property: JsonPropertyName("symbolCode")]    string SymbolCode,
    [property: JsonPropertyName("intervalId")]    int IntervalId,
    [property: JsonPropertyName("intervalCode")]  string IntervalCode,
    [property: JsonPropertyName("totalTimesteps")]      int TotalTimesteps,
    [property: JsonPropertyName("initialBalance")]      decimal InitialBalance,
    [property: JsonPropertyName("fee")]                 decimal Fee,
    [property: JsonPropertyName("slippage")]            decimal Slippage,
    [property: JsonPropertyName("dailyProfit")]         decimal DailyProfit,
    [property: JsonPropertyName("dailyDrawdownLimit")]  decimal DailyDrawdownLimit,
    [property: JsonPropertyName("maxCandlesPerTrade")]  int MaxCandlesPerTrade,
    [property: JsonPropertyName("riskPerTrade")]        decimal? RiskPerTrade,
    [property: JsonPropertyName("createdAt")]           long CreatedAt,
    [property: JsonPropertyName("trainingRunCount")]    int TrainingRunCount);
