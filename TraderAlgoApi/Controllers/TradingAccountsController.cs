using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.TradingAccounts;
using TraderAlgoApi.Services.TradingAccounts;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/trading-accounts")]
public sealed class TradingAccountsController(ITradingAccountService tradingAccountService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TradingAccountResponseDto>> Create(
        [FromBody] CreateTradingAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var account = await tradingAccountService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TradingAccountResponseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await tradingAccountService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<TradingAccountResponseDto>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await tradingAccountService.GetByIdAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<TradingAccountResponseDto>> Update(
        long id,
        [FromBody] UpdateTradingAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await tradingAccountService.UpdateAsync(id, request, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        try
        {
            await tradingAccountService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}
