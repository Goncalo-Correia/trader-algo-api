using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Backtests;
using TraderAlgoApi.Services.Backtests;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/backtests")]
public sealed class BacktestsController(IBacktestService backtestService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BacktestSummaryResponseDto>> Create(
        [FromBody] CreateBacktestRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await backtestService.CreateAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)       { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BacktestSummaryResponseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await backtestService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BacktestDetailResponseDto>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await backtestService.GetByIdAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        try
        {
            await backtestService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}
