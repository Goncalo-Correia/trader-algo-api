using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Services.Trades;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/trades")]
public sealed class TradesController(ITradeService tradeService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TradeResponseDto>> Create(
        [FromBody] CreateTradeRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var trade = await tradeService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetActive), new { tradingAccountId = trade.TradingAccountId }, trade);
        }
        catch (ArgumentException ex)      { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("{id:long}/stop")]
    public async Task<ActionResult<TradeResponseDto>> Stop(
        long id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await tradeService.StopAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<TradeResponseDto>> Update(
        long id,
        [FromBody] UpdateTradeRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await tradeService.UpdateAsync(id, request, cancellationToken));
        }
        catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<TradeResponseDto>>> GetActive(
        [FromQuery] long tradingAccountId,
        CancellationToken cancellationToken)
    {
        if (tradingAccountId <= 0)
            return BadRequest("tradingAccountId is required.");

        return Ok(await tradeService.GetActiveAsync(tradingAccountId, cancellationToken));
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<TradeResponseDto>>> GetHistory(
        [FromQuery] long tradingAccountId,
        CancellationToken cancellationToken)
    {
        if (tradingAccountId <= 0)
            return BadRequest("tradingAccountId is required.");

        return Ok(await tradeService.GetHistoryAsync(tradingAccountId, cancellationToken));
    }
}
