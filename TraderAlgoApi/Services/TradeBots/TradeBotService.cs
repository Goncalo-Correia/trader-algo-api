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
    private const int MlPolicyStrategyId = (int)TradingStrategy.MlPolicy;

    public async Task<TradeBotResponseDto> CreateAsync(
        CreateTradeBotRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.TradingAccounts
            .FirstOrDefaultAsync(a => a.Id == request.TradingAccountId, cancellationToken)
            ?? throw new ArgumentException($"Trading account {request.TradingAccountId} not found.");

        var strategyExists = await dbContext.TradingStrategies
            .AnyAsync(s => s.Id == request.TradingStrategyId, cancellationToken);
        if (!strategyExists)
            throw new ArgumentException($"Trading strategy {request.TradingStrategyId} not found.");

        if (request.IsEnabled)
        {
            if (!account.IsActive)
                throw new InvalidOperationException($"Trading account {request.TradingAccountId} is not active.");

            var alreadyEnabled = await dbContext.TradeBots
                .AnyAsync(
                    b => b.TradingAccountId == request.TradingAccountId &&
                         b.IsEnabled,
                    cancellationToken);
            if (alreadyEnabled)
                throw new InvalidOperationException(
                    $"Another tradebot is already enabled for trading account {request.TradingAccountId}. Disable it before enabling this one.");
        }

        var symbol = await LoadActiveSymbolAsync(request.SymbolCode, cancellationToken);
        var interval = await LoadActiveIntervalAsync(request.IntervalCode, cancellationToken);
        var policy = await ResolveMlPolicyForCreateAsync(request, symbol.Code, interval.Code, cancellationToken);
        if (policy is not null)
        {
            symbol = policy.Symbol;
            interval = policy.Interval;
        }

        var now = timeProvider.GetUtcNow();
        var tradeBot = new TradeBot
        {
            TradingAccountId   = account.Id,
            TradingStrategyId  = request.TradingStrategyId,
            MlPolicyId         = policy?.Id,
            SymbolId           = symbol.Id,
            IntervalId         = interval.Id,
            IsEnabled          = request.IsEnabled,
            IsNySessionOnly    = request.IsNySessionOnly,
            Delay              = request.Delay,
            CreatedAt          = now,
            UpdatedAt          = now
        };

        if (policy is null)
            ApplyRequestRisk(tradeBot, request);
        else
            ApplyPolicyRisk(tradeBot, policy);

        dbContext.TradeBots.Add(tradeBot);
        await dbContext.SaveChangesAsync(cancellationToken);

        tradeBot.TradingAccount = account;
        tradeBot.Symbol = symbol;
        tradeBot.Interval = interval;
        tradeBot.MlPolicy = policy;

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
        var policy = await ResolveMlPolicyForUpdateAsync(tradeBot, request, symbol.Code, interval.Code, cancellationToken);
        if (policy is not null)
        {
            symbol = policy.Symbol;
            interval = policy.Interval;
        }

        var wasEnabled = tradeBot.IsEnabled;

        tradeBot.SymbolId          = symbol.Id;
        tradeBot.IntervalId        = interval.Id;
        tradeBot.MlPolicyId        = policy?.Id;
        tradeBot.IsEnabled         = request.IsEnabled;
        tradeBot.IsNySessionOnly   = request.IsNySessionOnly;
        tradeBot.Delay             = request.Delay;
        tradeBot.UpdatedAt         = timeProvider.GetUtcNow();

        if (policy is null)
            ApplyRequestRisk(tradeBot, request);
        else
            ApplyPolicyRisk(tradeBot, policy);

        tradeBot.Symbol = symbol;
        tradeBot.Interval = interval;
        tradeBot.MlPolicy = policy;

        if (request.IsEnabled && !wasEnabled)
            await EnsureCanEnableAsync(tradeBot, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

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

        if (isEnabled && !tradeBot.IsEnabled)
            await EnsureCanEnableAsync(tradeBot, cancellationToken);

        tradeBot.IsEnabled = isEnabled;
        tradeBot.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        PublishBotStatusEvent(tradeBot);

        return ToDto(tradeBot);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var tradeBot = await TradeBotWithNavigations()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Tradebot {id} not found.");

        var wasEnabled = tradeBot.IsEnabled;

        dbContext.TradeBots.Remove(tradeBot);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (wasEnabled)
            PublishBotStatusEvent(tradeBot);
    }

    private IQueryable<TradeBot> TradeBotWithNavigations() =>
        dbContext.TradeBots
            .Include(b => b.TradingAccount)
            .Include(b => b.TradingStrategy)
            .Include(b => b.MlPolicy)
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

    private async Task<MlPolicy?> ResolveMlPolicyForCreateAsync(
        CreateTradeBotRequestDto request,
        string symbolCode,
        string intervalCode,
        CancellationToken cancellationToken)
    {
        if (request.TradingStrategyId != MlPolicyStrategyId)
        {
            if (request.MlPolicyId.HasValue)
                throw new ArgumentException("'mlPolicyId' is only valid for the ML Policy strategy.");

            return null;
        }

        if (request.MlPolicyId is not long policyId)
            throw new ArgumentException("'mlPolicyId' is required for the ML Policy strategy.");

        var policy = await LoadMlPolicyAsync(policyId, cancellationToken);
        ValidatePolicyMarket(policy, symbolCode, intervalCode);
        return policy;
    }

    private async Task<MlPolicy?> ResolveMlPolicyForUpdateAsync(
        TradeBot tradeBot,
        UpdateTradeBotRequestDto request,
        string symbolCode,
        string intervalCode,
        CancellationToken cancellationToken)
    {
        if (tradeBot.TradingStrategyId != MlPolicyStrategyId)
        {
            if (request.MlPolicyId.HasValue)
                throw new ArgumentException("'mlPolicyId' is only valid for the ML Policy strategy.");

            return null;
        }

        var policyId = request.MlPolicyId ?? tradeBot.MlPolicyId
            ?? throw new ArgumentException("'mlPolicyId' is required for the ML Policy strategy.");

        var policy = await LoadMlPolicyAsync(policyId, cancellationToken);
        ValidatePolicyMarket(policy, symbolCode, intervalCode);
        return policy;
    }

    private async Task<MlPolicy> LoadMlPolicyAsync(long policyId, CancellationToken cancellationToken) =>
        await dbContext.MlPolicies
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken)
        ?? throw new ArgumentException($"ML policy {policyId} not found.");

    private static void ValidatePolicyMarket(MlPolicy policy, string symbolCode, string intervalCode)
    {
        if (!string.Equals(policy.Symbol.Code, symbolCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(policy.Interval.Code, intervalCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"ML policy {policy.Id} is configured for {policy.Symbol.Code}/{policy.Interval.Code}; the tradebot must use the same symbol and interval.");
        }
    }

    private static void ApplyRequestRisk(TradeBot tradeBot, CreateTradeBotRequestDto request)
    {
        tradeBot.Quantity = request.Quantity;
        tradeBot.StopLoss = request.StopLoss;
        tradeBot.TakeProfit = request.TakeProfit;
        tradeBot.Breakeven = request.Breakeven;
        tradeBot.BreakevenStop = request.BreakevenStop;
        tradeBot.DailyProfitGoal = request.DailyProfitGoal;
        tradeBot.MaxLossesPerDay = request.MaxLossesPerDay;
        tradeBot.MaxCandlesPerTrade = request.MaxCandlesPerTrade;
        tradeBot.Fee = request.Fee;
    }

    private static void ApplyRequestRisk(TradeBot tradeBot, UpdateTradeBotRequestDto request)
    {
        tradeBot.Quantity = request.Quantity;
        tradeBot.StopLoss = request.StopLoss;
        tradeBot.TakeProfit = request.TakeProfit;
        tradeBot.Breakeven = request.Breakeven;
        tradeBot.BreakevenStop = request.BreakevenStop;
        tradeBot.DailyProfitGoal = request.DailyProfitGoal;
        tradeBot.MaxLossesPerDay = request.MaxLossesPerDay;
        tradeBot.MaxCandlesPerTrade = request.MaxCandlesPerTrade;
        tradeBot.Fee = request.Fee;
    }

    private static void ApplyPolicyRisk(TradeBot tradeBot, MlPolicy policy)
    {
        tradeBot.Quantity = policy.Quantity;
        tradeBot.StopLoss = policy.StopLoss;
        tradeBot.TakeProfit = policy.TakeProfit;
        tradeBot.Breakeven = policy.Breakeven;
        tradeBot.BreakevenStop = policy.BreakevenStop;
        tradeBot.DailyProfitGoal = policy.DailyProfit;
        tradeBot.MaxLossesPerDay = null;
        tradeBot.MaxCandlesPerTrade = policy.MaxCandlesPerTrade;
        tradeBot.Fee = policy.Fee;
    }

    private async Task EnsureCanEnableAsync(TradeBot tradeBot, CancellationToken cancellationToken)
    {
        if (tradeBot.TradingStrategyId == MlPolicyStrategyId)
        {
            if (tradeBot.MlPolicyId is not long policyId)
                throw new InvalidOperationException($"Tradebot {tradeBot.Id} uses ML Policy but has no linked ML policy.");

            var policyExists = tradeBot.MlPolicy is not null ||
                await dbContext.MlPolicies.AnyAsync(p => p.Id == policyId, cancellationToken);
            if (!policyExists)
                throw new InvalidOperationException($"ML policy {policyId} not found.");
        }

        if (tradeBot.TradingAccountId is long accountId)
        {
            if (tradeBot.TradingAccount?.IsActive != true)
                throw new InvalidOperationException($"Trading account {accountId} is not active.");

            var alreadyEnabled = await dbContext.TradeBots
                .AnyAsync(
                    b => b.Id != tradeBot.Id &&
                         b.TradingAccountId == accountId &&
                         b.IsEnabled,
                    cancellationToken);
            if (alreadyEnabled)
                throw new InvalidOperationException(
                    $"Another tradebot is already enabled for trading account {accountId}. Disable it before enabling this one.");

            return;
        }

        if (tradeBot.BacktestId is long backtestId)
        {
            var alreadyEnabled = await dbContext.TradeBots
                .AnyAsync(
                    b => b.Id != tradeBot.Id &&
                         b.BacktestId == backtestId &&
                         b.IsEnabled,
                    cancellationToken);
            if (alreadyEnabled)
                throw new InvalidOperationException(
                    $"Another tradebot is already enabled for backtest {backtestId}. Disable it before enabling this one.");

            return;
        }

        throw new InvalidOperationException($"Tradebot {tradeBot.Id} is not attached to a trading account or backtest.");
    }

    private static TradeBotResponseDto ToDto(TradeBot b) =>
        new(
            Id:                 b.Id,
            TradingAccountId:   b.TradingAccountId,
            TradingAccountName: b.TradingAccount?.Name,
            BacktestId:         b.BacktestId,
            TradingStrategy:    (TradingStrategy)b.TradingStrategyId,
            MlPolicyId:         b.MlPolicyId,
            SymbolCode:         b.Symbol.Code,
            IntervalCode:       b.Interval.Code,
            IsEnabled:          b.IsEnabled,
            Quantity:           b.Quantity,
            StopLoss:           b.StopLoss,
            TakeProfit:         b.TakeProfit,
            Breakeven:          b.Breakeven,
            BreakevenStop:      b.BreakevenStop,
            IsNySessionOnly:    b.IsNySessionOnly,
            Delay:              b.Delay,
            DailyProfitGoal:    b.DailyProfitGoal,
            MaxLossesPerDay:    b.MaxLossesPerDay,
            MaxCandlesPerTrade: b.MaxCandlesPerTrade,
            Fee:                b.Fee,
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
