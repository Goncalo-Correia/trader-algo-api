using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlTrainRequest(
    [property: JsonPropertyName("symbol")]    string Symbol,
    [property: JsonPropertyName("interval")]  string Interval,
    [property: JsonPropertyName("from_date")] string FromDate,
    [property: JsonPropertyName("to_date")]   string ToDate,
    [property: JsonPropertyName("model_id")]  string ModelId = "ppo-v1",
    // Set server-side before forwarding to Python so the training run can call back on completion.
    // Clients don't supply this; any value they send is overwritten.
    [property: JsonPropertyName("training_run_id")] long? TrainingRunId = null,
    // ── Training hyperparameters (forwarded to the Python trainer; null = Python default) ──
    [property: JsonPropertyName("total_timesteps")] int? TotalTimesteps = null,
    [property: JsonPropertyName("initial_balance")] decimal? InitialBalance = null,
    [property: JsonPropertyName("quantity")]        decimal? Quantity = null,
    [property: JsonPropertyName("stop_loss")]       decimal? StopLoss = null,
    [property: JsonPropertyName("take_profit")]     decimal? TakeProfit = null,
    [property: JsonPropertyName("breakeven")]       decimal? Breakeven = null,
    [property: JsonPropertyName("breakeven_stop")]  decimal? BreakevenStop = null,
    [property: JsonPropertyName("max_candles_per_trade")]   int? MaxCandlesPerTrade = null,
    [property: JsonPropertyName("daily_profit_target")]     decimal? DailyProfitTarget = null,
    [property: JsonPropertyName("daily_drawdown_limit")]    decimal? DailyDrawdownLimit = null,
    [property: JsonPropertyName("fee_rate")]                decimal? FeeRate = null,
    [property: JsonPropertyName("slippage_rate")]           decimal? SlippageRate = null,
    [property: JsonPropertyName("max_trailing_drawdown_threshold")] decimal? MaxTrailingDrawdownThreshold = null);
