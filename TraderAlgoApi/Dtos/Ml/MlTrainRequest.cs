using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlTrainRequest(
    [property: JsonPropertyName("ml_policy_id")] long MlPolicyId,
    [property: JsonPropertyName("training_run_id")] long TrainingRunId,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("interval")] string Interval,
    [property: JsonPropertyName("from_date")] string FromDate,
    [property: JsonPropertyName("to_date")] string ToDate,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("total_timesteps")] int TotalTimesteps,
    [property: JsonPropertyName("initial_balance")] decimal InitialBalance,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("stop_loss")] decimal StopLoss,
    [property: JsonPropertyName("take_profit")] decimal TakeProfit,
    [property: JsonPropertyName("breakeven")] decimal Breakeven,
    [property: JsonPropertyName("breakeven_stop")] decimal BreakevenStop,
    [property: JsonPropertyName("max_candles_per_trade")] int MaxCandlesPerTrade,
    [property: JsonPropertyName("daily_profit_target")] decimal DailyProfitTarget,
    [property: JsonPropertyName("daily_drawdown_limit")] decimal DailyDrawdownLimit,
    [property: JsonPropertyName("fee_rate")] decimal FeeRate,
    [property: JsonPropertyName("slippage_rate")] decimal SlippageRate,
    [property: JsonPropertyName("max_trailing_drawdown_threshold")] decimal MaxTrailingDrawdownThreshold);
