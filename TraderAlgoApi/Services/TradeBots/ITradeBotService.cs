using TraderAlgoApi.Dtos.TradeBots;

namespace TraderAlgoApi.Services.TradeBots;

public interface ITradeBotService
{
    Task<TradeBotResponseDto> CreateAsync(CreateTradeBotRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeBotResponseDto>> GetAllAsync(long? tradingAccountId = null, CancellationToken cancellationToken = default);

    Task<TradeBotResponseDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<TradeBotResponseDto> UpdateAsync(long id, UpdateTradeBotRequestDto request, CancellationToken cancellationToken = default);

    Task<TradeBotResponseDto> SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
