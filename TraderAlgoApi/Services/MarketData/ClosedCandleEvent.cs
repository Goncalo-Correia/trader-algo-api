namespace TraderAlgoApi.Services.MarketData;

public sealed record ClosedCandleEvent(
    string Symbol,
    string Interval,
    DateTimeOffset OpenTime,
    decimal High,
    decimal Low,
    decimal Close);
