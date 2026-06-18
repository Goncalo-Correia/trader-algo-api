namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlDecideQueryRequest(
    string? Symbol,
    string? Interval,
    string? ModelId = null);
