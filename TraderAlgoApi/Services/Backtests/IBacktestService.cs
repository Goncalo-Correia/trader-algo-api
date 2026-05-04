using TraderAlgoApi.Dtos.Backtests;

namespace TraderAlgoApi.Services.Backtests;

public interface IBacktestService
{
    Task<BacktestSummaryResponseDto> CreateAsync(CreateBacktestRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BacktestSummaryResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BacktestDetailResponseDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
