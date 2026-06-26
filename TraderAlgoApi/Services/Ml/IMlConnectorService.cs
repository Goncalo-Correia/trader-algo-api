using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Services.Ml;

public interface IMlConnectorService
{
    Task<MlDecideResponse> DecideAsync(
        MlDecideRequest request,
        CancellationToken cancellationToken = default);

    Task<MlTrainResponse> TrainAsync(
        MlTrainRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the models the ML service is currently serving (one per policy). This — not a
    /// Completed training run — is the source of truth for the live model, since promotion is gated.
    /// </summary>
    Task<IReadOnlyList<MlModelInfoResponse>> GetModelsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a run's deterministic training decision log. Returns null if the run has
    /// no decision log yet (e.g. training not finished / never run).
    /// </summary>
    Task<MlTrainingDecisionsResponse?> GetTrainingDecisionsAsync(
        long trainingRunId,
        CancellationToken cancellationToken = default);

    /// <summary>Best-effort delete of a run's decision log on the ML service.</summary>
    Task DeleteTrainingDecisionsAsync(
        long trainingRunId,
        CancellationToken cancellationToken = default);
}
