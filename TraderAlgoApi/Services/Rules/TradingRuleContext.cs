namespace TraderAlgoApi.Services.Rules;

public sealed record TradingRuleContext(
    string SymbolCode,
    string IntervalCode,
    decimal CurrentOpen,
    decimal CurrentHigh,
    decimal CurrentLow,
    decimal CurrentClose,
    decimal PreviousClose,
    decimal? CurrentSma20,
    decimal? CurrentSma100,
    decimal? PreviousSma20,
    decimal? PreviousSma100,
    decimal? CurrentRsi,
    decimal? CurrentRsiSmooth,
    decimal? PreviousRsi,
    decimal? PreviousRsiSmooth,
    decimal? CurrentMacdLine,
    decimal? CurrentSignalLine,
    decimal? CurrentHistogram,
    decimal? PreviousHistogram);
