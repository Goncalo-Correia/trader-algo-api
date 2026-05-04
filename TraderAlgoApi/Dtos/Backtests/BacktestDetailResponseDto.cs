using System.Text.Json.Serialization;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Dtos.Backtests;

public sealed record BacktestDetailResponseDto(
    [property: JsonPropertyName("id")]             long Id,
    [property: JsonPropertyName("tradeBotId")]     long? TradeBotId,
    [property: JsonPropertyName("symbolCode")]     string SymbolCode,
    [property: JsonPropertyName("intervalCode")]   string IntervalCode,
    [property: JsonPropertyName("strategyName")]   string StrategyName,
    [property: JsonPropertyName("from")]           long From,
    [property: JsonPropertyName("to")]             long To,
    [property: JsonPropertyName("startedAt")]      long StartedAt,
    [property: JsonPropertyName("completedAt")]    long? CompletedAt,
    [property: JsonPropertyName("status")]         BacktestStatus Status,
    [property: JsonPropertyName("initialBalance")] decimal InitialBalance,
    [property: JsonPropertyName("finalBalance")]   decimal? FinalBalance,
    [property: JsonPropertyName("pnl")]            decimal? Pnl,
    [property: JsonPropertyName("quantity")]       decimal Quantity,
    [property: JsonPropertyName("stopLoss")]       decimal? StopLoss,
    [property: JsonPropertyName("takeProfit")]     decimal? TakeProfit,
    [property: JsonPropertyName("breakeven")]      decimal? Breakeven,
    [property: JsonPropertyName("candleCount")]    int CandleCount,
    [property: JsonPropertyName("trades")]         IReadOnlyList<TradeResponseDto> Trades,
    [property: JsonPropertyName("candles")]        IReadOnlyList<CandleWithIndicatorsResponseDto> Candles,
    [property: JsonPropertyName("equityCurve")]    IReadOnlyList<EquityPointDto> EquityCurve);

public sealed record EquityPointDto(
    [property: JsonPropertyName("time")]    long Time,
    [property: JsonPropertyName("balance")] decimal Balance);
