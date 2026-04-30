using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.TradeEvents;
using TraderAlgoApi.Dtos.TradeBots;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.TradeEvents;

namespace TraderAlgoApi.Services.TradeBots;

public sealed class TradeBotService(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider,
    ITradeEventPublisher tradeEventPublisher) : ITradeBotService
{
    public async Task<TradeBotResponseDto> CreateAsync(
        CreateTradeBotRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.TradingAccounts
            .Include(a => a.TradingStrategy)
            .FirstOrDefaultAsync(a => a.Id == request.TradingAccountId, cancellationToken)
            ?? throw new ArgumentException($"Trading account {request.TradingAccountId} not found.");

        var existing = await dbContext.TradeBots
            .AnyAsync(b => b.TradingAccountId == request.TradingAccountId, cancellationToken);

        if (existing)
            throw new InvalidOperationException($"A tradebot already exists for trading account {request.TradingAccountId}.");

        var symbol = await LoadActiveSymbolAsync(request.SymbolCode, cancellationToken);
        var interval = await LoadActiveIntervalAsync(request.IntervalCode, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var tradeBot = new TradeBot
        {
            TradingAccountId = account.Id,
            SymbolId         = symbol.Id,
            IntervalId       = interval.Id,
            IsEnabled        = request.IsEnabled,
            Quantity         = request.Quantity,
            StopLoss         = request.StopLoss,
            TakeProfit       = request.TakeProfit,
            CreatedAt        = now,
            UpdatedAt        = now
        };

        dbContext.TradeBots.Add(tradeBot);
        await dbContext.SaveChangesAsync(cancellationToken);

        tradeBot.TradingAccount = account;
        tradeBot.Symbol = symbol;
        tradeBot.Interval = interval;

        return ToDto(tradeBot);
    }

    public async Task<IReadOnlyList<TradeBotResponseDto>> GetAllAsync(
        long? tradingAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var query = TradeBotWithNavigations().AsNoTracking();

        if (tradingAccountId is long accountId)
            query = query.Where(b => b.TradingAccountId == accountId);

        var tradeBots = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return tradeBots.Select(ToDto).ToList();
    }

    public async Task<TradeBotResponseDto> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tradeBot = await TradeBotWithNavigations()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Tradebot {id} not found.");

        return ToDto(tradeBot);
    }

    public async Task<TradeBotResponseDto> UpdateAsync(
        long id,
        UpdateTradeBotRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tradeBot = await TradeBotWithNavigations()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Tradebot {id} not found.");

        var symbol = await LoadActiveSymbolAsync(request.SymbolCode, cancellationToken);
        var interval = await LoadActiveIntervalAsync(request.IntervalCode, cancellationToken);
        var wasEnabled = tradeBot.IsEnabled;

        tradeBot.SymbolId = symbol.Id;
        tradeBot.IntervalId = interval.Id;
        tradeBot.IsEnabled = request.IsEnabled;
        tradeBot.Quantity = request.Quantity;
        tradeBot.StopLoss = request.StopLoss;
        tradeBot.TakeProfit = request.TakeProfit;
        tradeBot.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        tradeBot.Symbol = symbol;
        tradeBot.Interval = interval;

        if (wasEnabled != tradeBot.IsEnabled)
            PublishBotStatusEvent(tradeBot);

        return ToDto(tradeBot);
    }

    public async Task<TradeBotResponseDto> SetEnabledAsync(
        long id,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var tradeBot = await TradeBotWithNavigations()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Tradebot {id} not found.");

        if (isEnabled && !tradeBot.TradingAccount.IsActive)
            throw new InvalidOperationException($"Trading account {tradeBot.TradingAccountId} is not active.");

        tradeBot.IsEnabled = isEnabled;
        tradeBot.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        PublishBotStatusEvent(tradeBot);

        return ToDto(tradeBot);
    }

    private IQueryable<TradeBot> TradeBotWithNavigations() =>
        dbContext.TradeBots
            .Include(b => b.TradingAccount)
            .Include(b => b.Symbol)
            .Include(b => b.Interval);

    private async Task<Symbol> LoadActiveSymbolAsync(string symbolCode, CancellationToken cancellationToken) =>
        await dbContext.Symbols
            .FirstOrDefaultAsync(s => s.IsActive && s.Code == symbolCode, cancellationToken)
        ?? throw new ArgumentException($"Active symbol '{symbolCode}' not found.");

    private async Task<Interval> LoadActiveIntervalAsync(string intervalCode, CancellationToken cancellationToken) =>
        await dbContext.Intervals
            .FirstOrDefaultAsync(i => i.IsActive && i.Code == intervalCode, cancellationToken)
        ?? throw new ArgumentException($"Active interval '{intervalCode}' not found.");

    private static TradeBotResponseDto ToDto(TradeBot b) =>
        new(
            Id:                 b.Id,
            TradingAccountId:   b.TradingAccountId,
            TradingAccountName: b.TradingAccount.Name,
            TradingStrategy:    (TradingStrategy)b.TradingAccount.TradingStrategyId,
            SymbolCode:         b.Symbol.Code,
            IntervalCode:       b.Interval.Code,
            IsEnabled:          b.IsEnabled,
            Quantity:           b.Quantity,
            StopLoss:           b.StopLoss,
            TakeProfit:         b.TakeProfit,
            CreatedAt:          b.CreatedAt.ToUnixTimeMilliseconds(),
            UpdatedAt:          b.UpdatedAt.ToUnixTimeMilliseconds(),
            LastSignalAt:       b.LastSignalAt?.ToUnixTimeMilliseconds());

    private void PublishBotStatusEvent(TradeBot tradeBot)
    {
        tradeEventPublisher.Publish(new TradeEventDto(
            Type: tradeBot.IsEnabled ? "BotEnabled" : "BotDisabled",
            TradingAccountId: tradeBot.TradingAccountId,
            TradeId: null,
            SymbolCode: tradeBot.Symbol.Code,
            Message: tradeBot.IsEnabled ? "Tradebot enabled." : "Tradebot disabled.",
            CreatedAt: timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            Trade: null));
    }
}
