namespace TraderAlgoApi.Services.MarketData;

/// <summary>
/// Provider-neutral candle (OHLCV bar). Fields that have no equity equivalent
/// (TakerBuyBaseVolume, TakerBuyQuoteVolume) are stored as 0 for Alpaca bars.
/// QuoteAssetVolume stores dollar volume (volume × VWAP) for equities.
/// </summary>
public sealed record Candle(
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal QuoteAssetVolume,
    int     NumberOfTrades,
    decimal TakerBuyBaseVolume,
    decimal TakerBuyQuoteVolume);
