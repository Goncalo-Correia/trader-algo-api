using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Services.DataCollector;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/data-collector")]
public sealed class DataCollectorController(IDataCollectorService dataCollectorService) : ControllerBase
{
    [HttpPost("btc-usd")]
    public async Task<ActionResult<DataCollectionResult>> CollectBtcUsdKlines(CancellationToken cancellationToken)
    {
        var result = await dataCollectorService.CollectKlinesAsync("BTCUSD", "1h", cancellationToken);

        return Ok(result);
    }
}
