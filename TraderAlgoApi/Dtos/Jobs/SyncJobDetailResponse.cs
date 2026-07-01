using TraderAlgoApi.Models;

namespace TraderAlgoApi.Dtos.Jobs;

/// <summary>
/// A single sync job together with the collection errors linked to it.
/// </summary>
public sealed record SyncJobDetailResponse(
    SyncJobResponse Job,
    IReadOnlyList<SyncJobErrorResponse> Errors)
{
    public static SyncJobDetailResponse From(SyncJob job, IReadOnlyList<SyncJobError> errors) => new(
        SyncJobResponse.From(job),
        errors.Select(SyncJobErrorResponse.From).ToList());
}
