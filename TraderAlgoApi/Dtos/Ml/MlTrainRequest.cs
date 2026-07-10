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
    [property: JsonPropertyName("max_candles_per_trade")] int MaxCandlesPerTrade,
    [property: JsonPropertyName("daily_profit_target")] decimal DailyProfitTarget,
    [property: JsonPropertyName("daily_drawdown_limit")] decimal DailyDrawdownLimit,
    // slippage_rate is an ATR fraction: the per-fill price offset is slippage_rate × ATR-at-entry
    // (not a fixed price offset), applied on both entry and exit fills in the sidecar env.
    [property: JsonPropertyName("slippage_rate")] decimal SlippageRate,
    [property: JsonPropertyName("fee_rate")] decimal FeeRate,
    // Cash risked at the stop. The ML position size is risk_per_trade / stop_distance
    // (stop_distance = sl_atr_mult × ATR-at-entry), giving volatility-targeted sizing.
    [property: JsonPropertyName("risk_per_trade")] decimal? RiskPerTrade,
    // High-level validation scheme: one of the lowercase strings "single", "block", "sliding".
    // Detailed fold/window knobs are engine-owned defaults in the sidecar, not part of this contract.
    [property: JsonPropertyName("validation_scheme")] string ValidationScheme);
