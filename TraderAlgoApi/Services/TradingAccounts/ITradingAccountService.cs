using TraderAlgoApi.Dtos.TradingAccounts;

namespace TraderAlgoApi.Services.TradingAccounts;

public interface ITradingAccountService
{
    Task<TradingAccountResponseDto> CreateAsync(CreateTradingAccountRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradingAccountResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TradingAccountResponseDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<TradingAccountResponseDto> UpdateAsync(long id, UpdateTradingAccountRequestDto request, CancellationToken cancellationToken = default);
}
