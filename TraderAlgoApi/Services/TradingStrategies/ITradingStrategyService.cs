using TraderAlgoApi.Dtos.TradingStrategies;

namespace TraderAlgoApi.Services.TradingStrategies;

public interface ITradingStrategyService
{
    Task<IReadOnlyList<TradingStrategyResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
