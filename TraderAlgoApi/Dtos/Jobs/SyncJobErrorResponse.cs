using TraderAlgoApi.Models;

namespace TraderAlgoApi.Dtos.Jobs;

public sealed record SyncJobErrorResponse(
    long Id,
    string Symbol,
    string Interval,
    DateTimeOffset? CandleOpenTime,
    string Message,
    DateTimeOffset CreatedAt)
{
    public static SyncJobErrorResponse From(SyncJobError error) => new(
        error.Id,
        error.Symbol,
        error.Interval,
        error.CandleOpenTime,
        error.Message,
        error.CreatedAt);
}
