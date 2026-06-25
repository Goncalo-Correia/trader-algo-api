namespace TraderAlgoApi.Services.MarketData;

/// <summary>
/// Provider-neutral candle (OHLCV bar).
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
