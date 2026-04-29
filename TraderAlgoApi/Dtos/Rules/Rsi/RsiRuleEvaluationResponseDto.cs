namespace TraderAlgoApi.Dtos.Rules.Rsi;

public sealed record RsiRuleEvaluationResponseDto(
    string SymbolCode,
    string IntervalCode,
    decimal Close,
    decimal? Rsi,
    decimal? RsiSmooth,
    bool IsRsiBelow30,
    bool IsRsiAbove70,
    bool IsRsiAboveSmoothRsi,
    bool IsRsiBelowSmoothRsi,
    bool ShouldEnterLong,
    bool ShouldEnterShort);
