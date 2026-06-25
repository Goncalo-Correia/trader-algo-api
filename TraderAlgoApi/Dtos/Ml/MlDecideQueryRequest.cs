namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlDecideQueryRequest(
    long MlPolicyId,
    string? Symbol,
    string? Interval,
    string? ModelId = null);
