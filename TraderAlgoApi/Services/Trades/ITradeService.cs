using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.Trades;

public interface ITradeService
{
    Task<TradeResponseDto> CreateAsync(CreateTradeRequestDto request, CancellationToken cancellationToken = default);

    Task<TradeResponseDto> StopAsync(long id, CancellationToken cancellationToken = default);

    Task<TradeResponseDto> CloseAsync(long id, TradeCloseReason closeReason, CancellationToken cancellationToken = default);

    Task<TradeResponseDto> UpdateAsync(long id, UpdateTradeRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeResponseDto>> GetActiveAsync(long tradingAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeResponseDto>> GetHistoryAsync(long tradingAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeResponseDto>> GetByBacktestAsync(long backtestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called by <see cref="TradeMonitorService"/> on every price tick.
    /// Fills pending limit orders and triggers SL/TP on active trades.
    /// </summary>
    Task EvaluatePriceAsync(string symbol, decimal price, CancellationToken cancellationToken = default);
}
