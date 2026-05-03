using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.TradingStrategies;
using TraderAlgoApi.Services.TradingStrategies;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/trading-strategies")]
public sealed class TradingStrategiesController(ITradingStrategyService tradingStrategyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TradingStrategyResponseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await tradingStrategyService.GetAllAsync(cancellationToken));
    }
}
