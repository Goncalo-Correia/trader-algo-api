using TraderAlgoApi.Dtos.Charts;

namespace TraderAlgoApi.Services.Kronos;

public interface IKronosPredictService
{
    Task<IReadOnlyList<CandleResponseDto>> PredictAsync(
        string symbol,
        string interval,
        KronosPredictOptions options,
        CancellationToken cancellationToken = default);
}
