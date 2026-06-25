using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Services.Ml;

public interface IMlflowTrackingRepository
{
    Task<MlflowTrainingTrackingResponse> GetTrackingAsync(
        long trainingRunId,
        bool includeMetricHistory,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, MlflowTrainingTrackingSummaryDto>> GetTrackingSummariesAsync(
        IReadOnlyCollection<long> trainingRunIds,
        CancellationToken cancellationToken = default);
}
