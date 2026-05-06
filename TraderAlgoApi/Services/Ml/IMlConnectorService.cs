using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Services.Ml;

public interface IMlConnectorService
{
    Task<MlDecideResponse> DecideAsync(
        MlDecideRequest request,
        CancellationToken cancellationToken = default);
}
