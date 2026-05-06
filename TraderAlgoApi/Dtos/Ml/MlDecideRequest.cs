using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlDecideRequest(
    [property: JsonPropertyName("symbol")]         string Symbol,
    [property: JsonPropertyName("interval")]       string Interval,
    [property: JsonPropertyName("model_id")]       string ModelId,
    [property: JsonPropertyName("candle")]         MlCandleFeatures Candle,
    [property: JsonPropertyName("position")]       int Position,
    [property: JsonPropertyName("candles_held")]   int CandlesHeld,
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
