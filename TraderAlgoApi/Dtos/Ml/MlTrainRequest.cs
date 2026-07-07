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
    // ATR multipliers (evaluated against ATR at entry), not absolute price offsets. 0 disables breakeven.
    [property: JsonPropertyName("breakeven")] decimal Breakeven,
    [property: JsonPropertyName("breakeven_stop")] decimal BreakevenStop,
    [property: JsonPropertyName("max_candles_per_trade")] int MaxCandlesPerTrade,
    [property: JsonPropertyName("daily_profit_target")] decimal DailyProfitTarget,
    [property: JsonPropertyName("daily_drawdown_limit")] decimal DailyDrawdownLimit,
    [property: JsonPropertyName("fee_rate")] decimal FeeRate,
    // slippage_rate is an ATR fraction: the per-fill price offset is slippage_rate × ATR-at-entry
    // (not a fixed price offset), applied on both entry and exit fills in the sidecar env.
    [property: JsonPropertyName("slippage_rate")] decimal SlippageRate,

    // Optional risk sizing (§7). Cash risked at the stop; when set (> 0) the ML position size is
    // risk_per_trade / stop_distance (stop_distance = sl_atr_mult × ATR-at-entry), enabling
    // volatility-targeted sizing. Omitted when null so the sidecar falls back to the fixed quantity.
    [property: JsonPropertyName("risk_per_trade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? RiskPerTrade = null,

    // Optional tuning parameters (§3). Null values are omitted so the ML service applies its
    // own defaults, preserving prior behavior.
    [property: JsonPropertyName("episode_days"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? EpisodeDays = null,
    [property: JsonPropertyName("entry_cost"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? EntryCost = null,
    [property: JsonPropertyName("no_trade_day_penalty"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? NoTradeDayPenalty = null,
    [property: JsonPropertyName("streak_bonus_coef"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? StreakBonusCoef = null,
    [property: JsonPropertyName("max_streak_bonus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MaxStreakBonus = null,
    [property: JsonPropertyName("max_patience_reward_per_day"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MaxPatienceRewardPerDay = null,
    [property: JsonPropertyName("learning_rate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? LearningRate = null,
    [property: JsonPropertyName("n_steps"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NSteps = null,
    [property: JsonPropertyName("batch_size"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? BatchSize = null,
    [property: JsonPropertyName("n_epochs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NEpochs = null,
    [property: JsonPropertyName("gamma"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Gamma = null,
    [property: JsonPropertyName("gae_lambda"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? GaeLambda = null,
    [property: JsonPropertyName("clip_range"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ClipRange = null,
    [property: JsonPropertyName("ent_coef"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? EntCoef = null,
    [property: JsonPropertyName("oos_eval_every"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? OosEvalEvery = null);
