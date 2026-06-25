using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>Create/update body for a training policy. Symbol/interval are codes; model is an id.</summary>
public sealed record MlPolicyRequest(
    [property: JsonPropertyName("modelId")]   int ModelId,
    [property: JsonPropertyName("symbol")]    string Symbol,
    [property: JsonPropertyName("interval")]  string Interval,
    [property: JsonPropertyName("totalTimesteps")]      int TotalTimesteps,
    [property: JsonPropertyName("initialBalance")]      decimal InitialBalance,
    [property: JsonPropertyName("quantity")]            decimal Quantity,
    [property: JsonPropertyName("takeProfit")]          decimal? TakeProfit = null,
    [property: JsonPropertyName("stopLoss")]            decimal? StopLoss = null,
    [property: JsonPropertyName("breakeven")]           decimal? Breakeven = null,
    [property: JsonPropertyName("breakevenStop")]       decimal? BreakevenStop = null,
    [property: JsonPropertyName("fee")]                 decimal? Fee = null,
    [property: JsonPropertyName("slippage")]            decimal? Slippage = null,
    [property: JsonPropertyName("dailyProfit")]         decimal? DailyProfit = null,
    [property: JsonPropertyName("dailyDrawdownLimit")]  decimal? DailyDrawdownLimit = null,
    [property: JsonPropertyName("maxCandlesPerTrade")]  int? MaxCandlesPerTrade = null,
    [property: JsonPropertyName("maxTrailingDrawdown")] decimal? MaxTrailingDrawdown = null);

public sealed record MlPolicyResponse(
    [property: JsonPropertyName("id")]            long Id,
    [property: JsonPropertyName("modelId")]       int ModelId,
    [property: JsonPropertyName("modelName")]     string ModelName,
    [property: JsonPropertyName("symbolId")]      int SymbolId,
    [property: JsonPropertyName("symbolCode")]    string SymbolCode,
    [property: JsonPropertyName("intervalId")]    int IntervalId,
    [property: JsonPropertyName("intervalCode")]  string IntervalCode,
    [property: JsonPropertyName("totalTimesteps")]      int TotalTimesteps,
    [property: JsonPropertyName("initialBalance")]      decimal InitialBalance,
    [property: JsonPropertyName("quantity")]            decimal Quantity,
    [property: JsonPropertyName("takeProfit")]          decimal? TakeProfit,
    [property: JsonPropertyName("stopLoss")]            decimal? StopLoss,
    [property: JsonPropertyName("breakeven")]           decimal? Breakeven,
    [property: JsonPropertyName("breakevenStop")]       decimal? BreakevenStop,
    [property: JsonPropertyName("fee")]                 decimal? Fee,
    [property: JsonPropertyName("slippage")]            decimal? Slippage,
    [property: JsonPropertyName("dailyProfit")]         decimal? DailyProfit,
    [property: JsonPropertyName("dailyDrawdownLimit")]  decimal? DailyDrawdownLimit,
    [property: JsonPropertyName("maxCandlesPerTrade")]  int? MaxCandlesPerTrade,
    [property: JsonPropertyName("maxTrailingDrawdown")] decimal? MaxTrailingDrawdown,
    [property: JsonPropertyName("createdAt")]           long CreatedAt,
    [property: JsonPropertyName("trainingRunCount")]    int TrainingRunCount);

public sealed record MlModelResponse(
    [property: JsonPropertyName("id")]   int Id,
    [property: JsonPropertyName("name")] string Name);
