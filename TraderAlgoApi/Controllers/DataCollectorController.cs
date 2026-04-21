using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Services.DataCollector;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/data-collector")]
public sealed class DataCollectorController(IDataCollectorService dataCollectorService) : ControllerBase
{
    [HttpPost("{symbol}/{interval}")]
    public async Task<ActionResult<DataCollectionResult>> CollectKlines(
        string symbol,
        string interval,
        CancellationToken cancellationToken)
    {
        var result = await dataCollectorService.CollectKlinesAsync(symbol, interval, cancellationToken);

        return Ok(result);
    }
}
