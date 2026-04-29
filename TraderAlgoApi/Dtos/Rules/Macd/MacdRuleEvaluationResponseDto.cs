namespace TraderAlgoApi.Dtos.Rules.Macd;

public sealed record MacdRuleEvaluationResponseDto(
    string SymbolCode,
    string IntervalCode,
    decimal Close,
    decimal? MacdLine,
    decimal? SignalLine,
    decimal? Histogram,
    decimal? PreviousHistogram,
    bool IsMacdLineAboveSignalLine,
    bool IsMacdLineBelowSignalLine,
    bool IsHistogramAboveZero,
    bool IsHistogramBelowZero,
    bool IsHistogramIncreasing,
    bool IsHistogramDecreasing,
    bool ShouldEnterLong,
    bool ShouldEnterShort);
