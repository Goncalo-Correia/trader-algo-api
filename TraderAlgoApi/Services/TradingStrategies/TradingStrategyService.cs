using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.TradingStrategies;

namespace TraderAlgoApi.Services.TradingStrategies;

public sealed class TradingStrategyService(ApplicationDbContext dbContext) : ITradingStrategyService
{
    public async Task<IReadOnlyList<TradingStrategyResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var strategies = await dbContext.TradingStrategies
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return strategies
            .Select(s => new TradingStrategyResponseDto(s.Id, s.Name))
            .ToList();
    }
}
