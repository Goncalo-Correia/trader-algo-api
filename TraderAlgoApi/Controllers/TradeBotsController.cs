using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.TradeBots;
using TraderAlgoApi.Services.TradeBots;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/tradebots")]
public sealed class TradeBotsController(ITradeBotService tradeBotService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TradeBotResponseDto>> Create(
        [FromBody] CreateTradeBotRequestDto request,
        CancellationToken cancellationToken)
    {
        var tradeBot = await tradeBotService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tradeBot.Id }, tradeBot);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TradeBotResponseDto>>> GetAll(
        [FromQuery] long? tradingAccountId,
        CancellationToken cancellationToken)
    {
        return Ok(await tradeBotService.GetAllAsync(tradingAccountId, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<TradeBotResponseDto>> GetById(
        long id,
        CancellationToken cancellationToken) =>
        Ok(await tradeBotService.GetByIdAsync(id, cancellationToken));

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<TradeBotResponseDto>> Update(
        long id,
        [FromBody] UpdateTradeBotRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await tradeBotService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:long}/enable")]
    public async Task<ActionResult<TradeBotResponseDto>> Enable(
        long id,
        CancellationToken cancellationToken) =>
        Ok(await tradeBotService.SetEnabledAsync(id, true, cancellationToken));

    [HttpPost("{id:long}/disable")]
    public async Task<ActionResult<TradeBotResponseDto>> Disable(
        long id,
        CancellationToken cancellationToken) =>
        Ok(await tradeBotService.SetEnabledAsync(id, false, cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await tradeBotService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
