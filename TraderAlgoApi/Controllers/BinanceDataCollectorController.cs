using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Jobs;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.DataCollector;
using TraderAlgoApi.Services.Jobs;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/binance/data-collector")]
public sealed class BinanceDataCollectorController(
    IBinanceDataCollectorService dataCollectorService,
    ISyncJobService syncJobService) : ControllerBase
{
    /// <summary>
    /// Synchronous single symbol/interval collection. Targeted/diagnostic use — for a full backfill
    /// of every symbol/interval use <see cref="FullSync"/>, which runs as a background job.
    /// </summary>
    [HttpPost("{symbol}/{interval}")]
    public async Task<ActionResult<DataCollectionResult>> CollectKlines(
        string symbol,
        string interval,
        CancellationToken cancellationToken)
    {
        var result = await dataCollectorService.CollectKlinesAsync(
            symbol, interval, DataCollectorDefaults.DataStartDate, cancellationToken);
        return Ok(result);
    }

    [HttpPost("partial-sync")]
    public async Task<ActionResult<SyncJobResponse>> PartialSync(CancellationToken cancellationToken)
    {
        var job = await syncJobService.CreateAndQueueAsync(SyncJobType.DataCollectorPartialSync, cancellationToken);
        return Accepted(job);
    }

    [HttpPost("full-sync")]
    public async Task<ActionResult<SyncJobResponse>> FullSync(CancellationToken cancellationToken)
    {
        var job = await syncJobService.CreateAndQueueAsync(SyncJobType.DataCollectorFullSync, cancellationToken);
        return Accepted(job);
    }

    private ActionResult<SyncJobResponse> Accepted(Models.SyncJob job) =>
        AcceptedAtAction(nameof(JobsController.Get), "Jobs", new { id = job.Id }, SyncJobResponse.From(job));
}
