using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.TradingAccounts;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.TradingAccounts;

public sealed class TradingAccountService(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : ITradingAccountService
{
    public async Task<TradingAccountResponseDto> CreateAsync(
        CreateTradingAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var strategyExists = await dbContext.TradingStrategies
            .AnyAsync(s => s.Id == (int)request.TradingStrategy, cancellationToken);

        if (!strategyExists)
            throw new ArgumentException($"Trading strategy '{request.TradingStrategy}' not found.");

        var account = new TradingAccount
        {
            Name              = request.Name,
            InitialBalance    = request.InitialBalance,
            CurrentBalance    = request.InitialBalance,
            TradingStrategyId = (int)request.TradingStrategy,
            IsActive          = true,
            CreatedAt         = timeProvider.GetUtcNow()
        };

        dbContext.TradingAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(account).Reference(a => a.TradingStrategy).LoadAsync(cancellationToken);

        return ToDto(account);
    }

    public async Task<IReadOnlyList<TradingAccountResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = await dbContext.TradingAccounts
            .AsNoTracking()
            .Include(a => a.TradingStrategy)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts.Select(ToDto).ToList();
    }

    public async Task<TradingAccountResponseDto> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.TradingAccounts
            .Include(a => a.TradingStrategy)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Trading account {id} not found.");

        return ToDto(account);
    }

    public async Task<TradingAccountResponseDto> UpdateAsync(
        long id,
        UpdateTradingAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.TradingAccounts
            .Include(a => a.TradingStrategy)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Trading account {id} not found.");

        account.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(account);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.TradingAccounts
            .AnyAsync(a => a.Id == id, cancellationToken);

        if (!exists)
            throw new KeyNotFoundException($"Trading account {id} not found.");

        await dbContext.Trades
            .Where(t => t.TradingAccountId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.TradeBots
            .Where(b => b.TradingAccountId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.TradingAccounts
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static TradingAccountResponseDto ToDto(TradingAccount a) =>
        new(
            Id:              a.Id,
            Name:            a.Name,
            InitialBalance:  a.InitialBalance,
            CurrentBalance:  a.CurrentBalance,
            TradingStrategy: (TradingStrategy)a.TradingStrategyId,
            IsActive:        a.IsActive,
            CreatedAt:       a.CreatedAt.ToUnixTimeMilliseconds());
}
