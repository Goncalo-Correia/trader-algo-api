using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

/// <summary>
/// Deterministic decision log for a trained model, served by the Python ML service
/// (GET /training-runs/{model_id}/decisions). Replayed candle-by-candle so the
/// model's decision process can be visualized like an automated backtest.
/// </summary>
public sealed record MlTrainingDecisionsResponse(
    [property: JsonPropertyName("ml_policy_id")]   long MlPolicyId,
    [property: JsonPropertyName("training_run_id")] long TrainingRunId,
    [property: JsonPropertyName("model_id")]        string ModelId,
    [property: JsonPropertyName("symbol")]          string Symbol,
    [property: JsonPropertyName("interval")]        string Interval,
    [property: JsonPropertyName("from_date")]       string FromDate,
    [property: JsonPropertyName("to_date")]         string ToDate,
    [property: JsonPropertyName("initial_balance")] decimal InitialBalance,
    [property: JsonPropertyName("final_balance")]   decimal FinalBalance,
    [property: JsonPropertyName("pnl_pct")]         decimal PnlPct,
    [property: JsonPropertyName("oos_final_balance")] decimal? OosFinalBalance,
    [property: JsonPropertyName("oos_pnl_pct")]     decimal? OosPnlPct,
    [property: JsonPropertyName("n_trades")]        int NTrades,
    [property: JsonPropertyName("continued_from_training_run_id")] long? ContinuedFromTrainingRunId,
    [property: JsonPropertyName("policy_params")] IReadOnlyDictionary<string, JsonElement> PolicyParams,
    [property: JsonPropertyName("decisions")]       IReadOnlyList<MlTrainingDecision> Decisions,
    [property: JsonPropertyName("trades")]          IReadOnlyList<MlTrainingTrade> Trades);

public sealed record MlTrainingDecision(
    [property: JsonPropertyName("candle_index")] int CandleIndex,
    [property: JsonPropertyName("open_time")]    long? OpenTime,
    [property: JsonPropertyName("action")]       int Action,
    [property: JsonPropertyName("action_name")]  string ActionName,
    [property: JsonPropertyName("confidence")]   double Confidence,
    [property: JsonPropertyName("probs")]        IReadOnlyList<double> Probs,
    [property: JsonPropertyName("position")]     int Position,
    [property: JsonPropertyName("balance")]      decimal Balance);

public sealed record MlTrainingTrade(
    [property: JsonPropertyName("entry_step")]  int EntryStep,
    [property: JsonPropertyName("entry_time")]  long? EntryTime,
    [property: JsonPropertyName("entry_price")] decimal EntryPrice,
    [property: JsonPropertyName("side")]        string Side,
    [property: JsonPropertyName("exit_step")]   int ExitStep,
    [property: JsonPropertyName("exit_time")]   long? ExitTime,
    [property: JsonPropertyName("exit_price")]  decimal ExitPrice,
    [property: JsonPropertyName("reason")]      string Reason,
    [property: JsonPropertyName("pnl")]         decimal Pnl);
