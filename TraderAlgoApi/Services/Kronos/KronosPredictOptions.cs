namespace TraderAlgoApi.Services.Kronos;

public sealed record KronosPredictOptions(
    string ModelId,
    int Lookback,
    int PredLen,
    double Temperature,
    int TopK,
    double TopP,
    int SampleCount);
