using TraderAlgoApi.Dtos.Trades;

namespace TraderAlgoApi.Services.Trades;

public interface ITradeService
{
    Task<TradeResponseDto> CreateAsync(CreateTradeRequestDto request, CancellationToken cancellationToken = default);

    Task<TradeResponseDto> StopAsync(long id, CancellationToken cancellationToken = default);

    Task<TradeResponseDto> UpdateAsync(long id, UpdateTradeRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeResponseDto>> GetActiveAsync(string symbol, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeResponseDto>> GetHistoryAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called by <see cref="TradeMonitorService"/> on every price tick.
    /// Fills pending limit orders and triggers SL/TP on active trades.
    /// </summary>
    Task EvaluatePriceAsync(string symbol, decimal price, CancellationToken cancellationToken = default);
}
