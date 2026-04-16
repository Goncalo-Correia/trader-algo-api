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
        return await CollectBtcUsdKlinesByInterval("1h", cancellationToken);
    }

    [HttpPost("btc-usd/{interval}")]
    public async Task<ActionResult<DataCollectionResult>> CollectBtcUsdKlinesByInterval(
        string interval,
        CancellationToken cancellationToken)
    {
        var result = await dataCollectorService.CollectKlinesAsync("BTCUSD", interval, cancellationToken);

        return Ok(result);
    }
}
