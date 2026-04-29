namespace TraderAlgoApi.Dtos.Rules.Sma;

public sealed record SmaRuleEvaluationResponseDto(
    string SymbolCode,
    string IntervalCode,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal? Sma20,
    decimal? Sma100,
    bool IsSma20AboveSma100,
    bool IsSma20BelowSma100,
    bool IsPriceRetestingSma20,
    bool ShouldEnterLong,
    bool ShouldEnterShort);
