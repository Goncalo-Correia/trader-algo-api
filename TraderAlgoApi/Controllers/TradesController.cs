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
        var trade = await tradeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetActive), new { tradingAccountId = trade.TradingAccountId ?? 0 }, trade);
    }

    [HttpPost("{id:long}/stop")]
    public async Task<ActionResult<TradeResponseDto>> Stop(
        long id,
        CancellationToken cancellationToken) =>
        Ok(await tradeService.StopAsync(id, cancellationToken));

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<TradeResponseDto>> Update(
        long id,
        [FromBody] UpdateTradeRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await tradeService.UpdateAsync(id, request, cancellationToken));

    [HttpGet("account/{tradingAccountId:long}/active")]
    public async Task<ActionResult<IReadOnlyList<TradeResponseDto>>> GetActive(
        long tradingAccountId,
        CancellationToken cancellationToken)
    {
        return Ok(await tradeService.GetActiveAsync(tradingAccountId, cancellationToken));
    }

    [HttpGet("account/{tradingAccountId:long}/history")]
    public async Task<ActionResult<IReadOnlyList<TradeResponseDto>>> GetHistory(
        long tradingAccountId,
        CancellationToken cancellationToken)
    {
        return Ok(await tradeService.GetHistoryAsync(tradingAccountId, cancellationToken));
    }

    [HttpGet("backtest/{backtestId:long}")]
    public async Task<ActionResult<IReadOnlyList<TradeResponseDto>>> GetByBacktest(
        long backtestId,
        CancellationToken cancellationToken)
    {
        return Ok(await tradeService.GetByBacktestAsync(backtestId, cancellationToken));
    }
}
