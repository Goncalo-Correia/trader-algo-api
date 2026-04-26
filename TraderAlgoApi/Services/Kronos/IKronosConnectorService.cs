using TraderAlgoApi.Dtos.Kronos;

namespace TraderAlgoApi.Services.Kronos;

public interface IKronosConnectorService
{
    Task<KronosPredictResponse> PredictAsync(KronosPredictRequest request, CancellationToken cancellationToken = default);
}
