using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Charts;

public sealed record CandleWithIndicatorsResponseDto(
    // ── Candle ────────────────────────────────────────────────────────────────
    [property: JsonPropertyName("time")]   long    Time,
    [property: JsonPropertyName("open")]   decimal Open,
    [property: JsonPropertyName("high")]   decimal High,
    [property: JsonPropertyName("low")]    decimal Low,
    [property: JsonPropertyName("close")]  decimal Close,
    [property: JsonPropertyName("volume")]                        decimal Volume,
    [property: JsonPropertyName("taker_buy_base_asset_volume")]  decimal TakerBuyBaseAssetVolume,
    [property: JsonPropertyName("taker_sell_base_asset_volume")] decimal TakerSellBaseAssetVolume,

    // ── Simple Moving Average ─────────────────────────────────────────────────
    [property: JsonPropertyName("sma_20")]  decimal? Sma20,
    [property: JsonPropertyName("sma_100")] decimal? Sma100,

    // ── Relative Strength Index ───────────────────────────────────────────────
    [property: JsonPropertyName("rsi")]        decimal? Rsi,
    [property: JsonPropertyName("rsi_smooth")] decimal? RsiSmooth,
    [property: JsonPropertyName("rsi_divergence")] bool?    Divergence,

    // ── MACD ──────────────────────────────────────────────────────────────────
    [property: JsonPropertyName("macd_line")]    decimal? MacdLine,
    [property: JsonPropertyName("macd_signal_line")]  decimal? SignalLine,
    [property: JsonPropertyName("macd_histogram")] decimal? Histogram,

    // ── Average True Range ────────────────────────────────────────────────────
    [property: JsonPropertyName("atr_period")]     int?     AtrPeriod,
    [property: JsonPropertyName("atr_true_range")] decimal? AtrTrueRange,
    [property: JsonPropertyName("atr")]            decimal? Atr);
