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
        CancellationToken cancellationToken) =>
        Ok(await backtestService.CreateAsync(request, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BacktestSummaryResponseDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await backtestService.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BacktestDetailResponseDto>> GetById(
        long id,
        CancellationToken cancellationToken) =>
        Ok(await backtestService.GetByIdAsync(id, cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await backtestService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
