using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Jobs;
using TraderAlgoApi.Services.Jobs;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ISyncJobService syncJobService) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<ActionResult<SyncJobResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var job = await syncJobService.GetAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(SyncJobResponse.From(job));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SyncJobResponse>>> List(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var jobs = await syncJobService.ListRecentAsync(Math.Clamp(take, 1, 100), cancellationToken);
        return Ok(jobs.Select(SyncJobResponse.From).ToList());
    }
}
