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
        var account = await tradingAccountService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
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
        CancellationToken cancellationToken) =>
        Ok(await tradingAccountService.GetByIdAsync(id, cancellationToken));

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<TradingAccountResponseDto>> Update(
        long id,
        [FromBody] UpdateTradingAccountRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await tradingAccountService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await tradingAccountService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
