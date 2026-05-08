using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Services.Indicators;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/indicators")]
public sealed class IndicatorsController(
    IIndicatorSyncService indicatorSyncService) : ControllerBase
{
    [HttpPost("full-sync")]
    public async Task<ActionResult<IReadOnlyList<IndicatorSyncResult>>> FullSync(CancellationToken cancellationToken)
    {
        var results = await indicatorSyncService.FullSyncAsync(cancellationToken);
        return Ok(results);
    }

    [HttpPost("partial-sync")]
    public async Task<ActionResult<IReadOnlyList<IndicatorSyncResult>>> PartialSync(CancellationToken cancellationToken)
    {
        var results = await indicatorSyncService.PartialSyncAsync(cancellationToken);
        return Ok(results);
    }
}
