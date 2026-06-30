using TraderAlgoApi.Models;

namespace TraderAlgoApi.Dtos.Jobs;

public sealed record SyncJobResponse(
    long Id,
    string Type,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int TotalUnits,
    int CompletedUnits,
    string? Message,
    string? Error)
{
    public static SyncJobResponse From(SyncJob job) => new(
        job.Id,
        job.TypeEnum.ToString(),
        job.StatusEnum.ToString(),
        job.CreatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.TotalUnits,
        job.CompletedUnits,
        job.Message,
        job.Error);
}
