using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Jobs;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Jobs;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/indicators")]
public sealed class IndicatorsController(ISyncJobService syncJobService) : ControllerBase
{
    [HttpPost("full-sync")]
    public async Task<ActionResult<SyncJobResponse>> FullSync(CancellationToken cancellationToken)
    {
        var job = await syncJobService.CreateAndQueueAsync(SyncJobType.IndicatorFullSync, cancellationToken);
        return Accepted(job);
    }

    [HttpPost("partial-sync")]
    public async Task<ActionResult<SyncJobResponse>> PartialSync(CancellationToken cancellationToken)
    {
        var job = await syncJobService.CreateAndQueueAsync(SyncJobType.IndicatorPartialSync, cancellationToken);
        return Accepted(job);
    }

    private ActionResult<SyncJobResponse> Accepted(Models.SyncJob job) =>
        AcceptedAtAction(nameof(JobsController.Get), "Jobs", new { id = job.Id }, SyncJobResponse.From(job));
}
