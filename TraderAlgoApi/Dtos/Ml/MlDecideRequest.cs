using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlDecideRequest(
    [property: JsonPropertyName("ml_policy_id")] long MlPolicyId,
    [property: JsonPropertyName("symbol")]         string Symbol,
    [property: JsonPropertyName("interval")]       string Interval,
    [property: JsonPropertyName("model_id")]       string ModelId,
    [property: JsonPropertyName("candle")]         MlCandleFeatures Candle,
    [property: JsonPropertyName("position")]       int Position,
    [property: JsonPropertyName("initial_account_balance")] decimal InitialAccountBalance,
    [property: JsonPropertyName("current_account_balance")] decimal CurrentAccountBalance,
    [property: JsonPropertyName("current_daily_pnl")] decimal CurrentDailyPnl,
    [property: JsonPropertyName("current_daily_drawdown")] decimal CurrentDailyDrawdown,
    [property: JsonPropertyName("wins_in_row")] int WinsInRow,
    [property: JsonPropertyName("losses_in_row")] int LossesInRow,
    [property: JsonPropertyName("trades_taken_today")] int TradesTakenToday,
    [property: JsonPropertyName("daily_profit_target_reached")] bool DailyProfitTargetReached,
    [property: JsonPropertyName("daily_drawdown_reached")] bool DailyDrawdownReached,
    [property: JsonPropertyName("last_trade_pnl")] decimal LastTradePnl,
    [property: JsonPropertyName("last_trade_close_reason")] string LastTradeCloseReason,
    [property: JsonPropertyName("candles_since_last_trade_closed")] int CandlesSinceLastTradeClosed,
    // ATR multipliers (evaluated against ATR at entry), not absolute price offsets. 0 disables breakeven.
    [property: JsonPropertyName("configured_breakeven")] decimal ConfiguredBreakeven,
    [property: JsonPropertyName("configured_breakeven_stop")] decimal ConfiguredBreakevenStop,
    [property: JsonPropertyName("configured_max_candles_per_trade")] int ConfiguredMaxCandlesPerTrade,
    [property: JsonPropertyName("fee_rate")] decimal FeeRate,
    [property: JsonPropertyName("unrealized_pnl")] decimal UnrealizedPnl);

public sealed record MlCandleFeatures(
    [property: JsonPropertyName("open")]             decimal Open,
    [property: JsonPropertyName("high")]             decimal High,
    [property: JsonPropertyName("low")]              decimal Low,
    [property: JsonPropertyName("close")]            decimal Close,
    [property: JsonPropertyName("volume")]           decimal Volume,
    [property: JsonPropertyName("taker_buy_volume")] decimal TakerBuyVolume,
    [property: JsonPropertyName("sma20")]            decimal? Sma20,
    [property: JsonPropertyName("sma100")]           decimal? Sma100,
    [property: JsonPropertyName("rsi")]              decimal? Rsi,
    [property: JsonPropertyName("rsi_smooth")]       decimal? RsiSmooth,
    [property: JsonPropertyName("macd_line")]        decimal? MacdLine,
    [property: JsonPropertyName("signal_line")]      decimal? SignalLine,
    [property: JsonPropertyName("histogram")]        decimal? Histogram);
