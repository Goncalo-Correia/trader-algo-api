using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Backtests;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.Backtests;

public sealed class BacktestService(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : IBacktestService
{
    public async Task<BacktestSummaryResponseDto> CreateAsync(
        CreateBacktestRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var symbol = await dbContext.Symbols
            .FirstOrDefaultAsync(s => s.Code == request.SymbolCode, cancellationToken)
            ?? throw new ArgumentException($"Symbol '{request.SymbolCode}' not found.");

        var interval = await dbContext.Intervals
            .FirstOrDefaultAsync(i => i.Code == request.IntervalCode, cancellationToken)
            ?? throw new ArgumentException($"Interval '{request.IntervalCode}' not found.");

        if (request.From >= request.To)
            throw new ArgumentException("'from' must be earlier than 'to'.");

        var templateBot = await LoadTemplateBotAsync(request, cancellationToken);
        var tradingStrategyId = request.TradingStrategyId ?? templateBot.TradingStrategyId;
        var mlPolicy = await ResolveMlPolicyAsync(request, templateBot, tradingStrategyId, symbol.Code, interval.Code, cancellationToken);
        var quantity = mlPolicy?.Quantity ?? request.Quantity ?? templateBot.Quantity;
        var stopLoss = mlPolicy?.StopLoss ?? request.StopLoss ?? templateBot.StopLoss;
        var takeProfit = mlPolicy?.TakeProfit ?? request.TakeProfit ?? templateBot.TakeProfit;
        var breakeven = mlPolicy?.Breakeven ?? request.Breakeven;
        var breakevenStop = mlPolicy?.BreakevenStop ?? request.BreakevenStop;
        var fee = mlPolicy?.Fee ?? request.Fee;
        var dailyProfitGoal = mlPolicy?.DailyProfit ?? request.DailyProfitGoal;
        var maxCandlesPerTrade = mlPolicy?.MaxCandlesPerTrade ?? request.MaxCandlesPerTrade;

        if (quantity <= 0)
            throw new ArgumentException("'quantity' must be greater than zero.");

        var strategy = await dbContext.TradingStrategies
            .FirstOrDefaultAsync(s => s.Id == tradingStrategyId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Strategy {tradingStrategyId} not found.");

        var now = timeProvider.GetUtcNow();

        // Backtest + its template TradeBot are created as a unit — wrap them in a transaction so a
        // failure between the saves can't leave a backtest without its bot (or vice versa).
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var backtest = new Backtest
        {
            SymbolId   = symbol.Id,
            IntervalId = interval.Id,
            From       = request.From,
            To                = request.To,
            StartedAt         = now,
            StatusId          = (int)BacktestStatus.Pending,
            InitialBalance    = request.InitialBalance,
            CandleCount       = 0
        };

        dbContext.Backtests.Add(backtest);
        await dbContext.SaveChangesAsync(cancellationToken);

        var tradeBot = new TradeBot
        {
            BacktestId         = backtest.Id,
            TradingStrategyId  = tradingStrategyId,
            MlPolicyId         = mlPolicy?.Id,
            SymbolId           = symbol.Id,
            IntervalId         = interval.Id,
            IsEnabled          = true,
            Quantity           = quantity,
            StopLoss           = stopLoss,
            TakeProfit         = takeProfit,
            Breakeven          = breakeven,
            BreakevenStop      = breakevenStop,
            Fee                = fee,
            IsNySessionOnly    = request.IsNySessionOnly,
            DailyProfitGoal    = dailyProfitGoal,
            MaxLossesPerDay    = request.MaxLossesPerDay,
            MaxCandlesPerTrade = maxCandlesPerTrade,
            CreatedAt          = now,
            UpdatedAt          = now
        };

        dbContext.TradeBots.Add(tradeBot);
        await dbContext.SaveChangesAsync(cancellationToken);

        backtest.TradeBotId = tradeBot.Id;
        backtest.TradeBot = tradeBot;
        tradeBot.TradingStrategy = strategy;
        tradeBot.MlPolicy = mlPolicy;
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ToSummaryDto(backtest, symbol.Code, interval.Code, strategy.Name, tradeBot, tradeCount: 0,
            closedTrades: []);
    }

    public async Task<IReadOnlyList<BacktestSummaryResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        // Project to a lightweight shape: the summary only needs each trade's ClosedAt + Pnl to
        // build the equity curve / drawdowns, so we avoid loading full Trade graphs for every backtest.
        var rows = await dbContext.Backtests
            .AsNoTracking()
            .OrderByDescending(b => b.StartedAt)
            .Select(b => new
            {
                Backtest     = b,
                SymbolCode   = b.Symbol.Code,
                IntervalCode = b.Interval.Code,
                StrategyName = b.TradeBot != null && b.TradeBot.TradingStrategy != null
                    ? b.TradeBot.TradingStrategy.Name
                    : string.Empty,
                Bot          = b.TradeBot,
                TradeCount   = b.Trades.Count,
                ClosedTrades = b.Trades
                    .Where(t => t.Pnl != null && t.ClosedAt != null)
                    .Select(t => new { t.ClosedAt, t.Pnl })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => ToSummaryDto(
                r.Backtest, r.SymbolCode, r.IntervalCode, r.StrategyName, r.Bot, r.TradeCount,
                r.ClosedTrades.Select(c => (c.ClosedAt, c.Pnl))))
            .ToList();
    }

    public async Task<BacktestDetailResponseDto> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var backtest = await dbContext.Backtests
            .AsNoTracking()
            .Include(b => b.Symbol)
            .Include(b => b.Interval)
            .Include(b => b.TradeBot)
                .ThenInclude(tb => tb!.TradingStrategy)
            .Include(b => b.Trades)
                .ThenInclude(t => t.Symbol)
            .Include(b => b.Trades)
                .ThenInclude(t => t.Interval)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Backtest {id} not found.");

        var tradeDtos = backtest.Trades
            .OrderBy(t => t.OpenedAt)
            .Select(t => TradeToDto(t, t.AccountPnl))
            .ToList();

        var equity = BacktestSimulationEngine.BuildEquityCurve(
            backtest.InitialBalance,
            backtest.From.ToUnixTimeSeconds(),
            backtest.Trades.Select(t => (t.ClosedAt, t.Pnl)));
        var (maxDrawdown, maxTrailingDrawdown) =
            BacktestSimulationEngine.ComputeDrawdowns(equity, backtest.InitialBalance);

        var bot = backtest.TradeBot;

        return new BacktestDetailResponseDto(
            Id:             backtest.Id,
            TradeBotId:     backtest.TradeBotId,
            SymbolCode:     backtest.Symbol.Code,
            IntervalCode:   backtest.Interval.Code,
            StrategyName:   bot?.TradingStrategy?.Name ?? string.Empty,
            From:           backtest.From.ToUnixTimeSeconds(),
            To:             backtest.To.ToUnixTimeSeconds(),
            StartedAt:      backtest.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    backtest.CompletedAt?.ToUnixTimeMilliseconds(),
            Status:         backtest.StatusEnum,
            InitialBalance: backtest.InitialBalance,
            FinalBalance:   backtest.FinalBalance,
            Pnl:            backtest.Pnl,
            Quantity:       bot?.Quantity ?? 0,
            StopLoss:       bot?.StopLoss,
            TakeProfit:     bot?.TakeProfit,
            Breakeven:      bot?.Breakeven,
            BreakevenStop:  bot?.BreakevenStop,
            IsNySessionOnly: bot?.IsNySessionOnly ?? false,
            Delay:          bot?.Delay ?? false,
            DailyProfitGoal: bot?.DailyProfitGoal,
            MaxLossesPerDay: bot?.MaxLossesPerDay,
            MaxCandlesPerTrade: bot?.MaxCandlesPerTrade,
            CandleCount:    backtest.CandleCount,
            MaxDrawdown:    maxDrawdown,
            MaxTrailingDrawdown: maxTrailingDrawdown,
            Trades:         tradeDtos,
            EquityCurve:    equity);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Backtests
            .AnyAsync(b => b.Id == id, cancellationToken);

        if (!exists)
            throw new KeyNotFoundException($"Backtest {id} not found.");

        // Three dependent deletes — run them in one transaction so a mid-sequence failure can't
        // leave orphaned trades or bots behind.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Trades
            .Where(t => t.BacktestId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.TradeBots
            .Where(b => b.BacktestId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Backtests
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<TradeBot> LoadTemplateBotAsync(
        CreateBacktestRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.TradingStrategyId.HasValue &&
            (request.Quantity.HasValue || request.TradingStrategyId.Value == (int)TradingStrategy.MlPolicy))
        {
            return new TradeBot
            {
                TradingStrategyId = request.TradingStrategyId.Value,
                MlPolicyId = request.MlPolicyId,
                Quantity = request.Quantity ?? 0m,
                StopLoss = request.StopLoss,
                TakeProfit = request.TakeProfit
            };
        }

        return await dbContext.TradeBots
            .Include(b => b.TradingAccount)
            .Include(b => b.MlPolicy).ThenInclude(p => p!.Symbol)
            .Include(b => b.MlPolicy).ThenInclude(p => p!.Interval)
            .Where(b => b.IsEnabled &&
                        b.BacktestId == null &&
                        b.TradingAccountId != null &&
                        b.TradingAccount != null &&
                        b.TradingAccount.IsActive)
            .OrderByDescending(b => b.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Provide tradingStrategy and quantity, or enable an account tradebot to use as the backtest template.");
    }

    private async Task<MlPolicy?> ResolveMlPolicyAsync(
        CreateBacktestRequestDto request,
        TradeBot templateBot,
        int tradingStrategyId,
        string symbolCode,
        string intervalCode,
        CancellationToken cancellationToken)
    {
        if (tradingStrategyId != (int)TradingStrategy.MlPolicy)
        {
            if (request.MlPolicyId.HasValue)
                throw new ArgumentException("'mlPolicyId' is only valid for the ML Policy strategy.");

            return null;
        }

        var policyId = request.MlPolicyId ?? templateBot.MlPolicyId
            ?? throw new ArgumentException("'mlPolicyId' is required for the ML Policy strategy.");

        var policy = await dbContext.MlPolicies
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken)
            ?? throw new ArgumentException($"ML policy {policyId} not found.");

        if (!string.Equals(policy.Symbol.Code, symbolCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(policy.Interval.Code, intervalCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"ML policy {policy.Id} is configured for {policy.Symbol.Code}/{policy.Interval.Code}; the backtest must use the same symbol and interval.");
        }

        return policy;
    }

    private static BacktestSummaryResponseDto ToSummaryDto(
        Backtest b,
        string symbolCode,
        string intervalCode,
        string strategyName,
        TradeBot? bot,
        int tradeCount,
        IEnumerable<(DateTimeOffset? ClosedAt, decimal? Pnl)> closedTrades)
    {
        var equity = BacktestSimulationEngine.BuildEquityCurve(
            b.InitialBalance, b.From.ToUnixTimeSeconds(), closedTrades);
        var (maxDrawdown, maxTrailingDrawdown) =
            BacktestSimulationEngine.ComputeDrawdowns(equity, b.InitialBalance);

        return new(
            Id:             b.Id,
            TradeBotId:     b.TradeBotId,
            SymbolCode:     symbolCode,
            IntervalCode:   intervalCode,
            StrategyName:   strategyName,
            From:           b.From.ToUnixTimeSeconds(),
            To:             b.To.ToUnixTimeSeconds(),
            StartedAt:      b.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    b.CompletedAt?.ToUnixTimeMilliseconds(),
            Status:         b.StatusEnum,
            InitialBalance: b.InitialBalance,
            FinalBalance:   b.FinalBalance,
            Pnl:            b.Pnl,
            Quantity:       bot?.Quantity ?? 0,
            StopLoss:       bot?.StopLoss,
            TakeProfit:     bot?.TakeProfit,
            Breakeven:      bot?.Breakeven,
            BreakevenStop:  bot?.BreakevenStop,
            IsNySessionOnly: bot?.IsNySessionOnly ?? false,
            Delay:          bot?.Delay ?? false,
            DailyProfitGoal: bot?.DailyProfitGoal,
            MaxLossesPerDay: bot?.MaxLossesPerDay,
            MaxCandlesPerTrade: bot?.MaxCandlesPerTrade,
            CandleCount:    b.CandleCount,
            TradeCount:     tradeCount,
            MaxDrawdown:    maxDrawdown,
            MaxTrailingDrawdown: maxTrailingDrawdown);
    }

    private static TradeResponseDto TradeToDto(Trade t, decimal? accountPnl = null) =>
        new(
            Id:               t.Id,
            SymbolCode:       t.Symbol?.Code ?? string.Empty,
            IntervalCode:     t.Interval?.Code,
            Side:             t.SideEnum,
            OrderType:        t.OrderTypeEnum,
            Quantity:         t.Quantity,
            RequestedPrice:   t.RequestedPrice,
            EntryPrice:       t.EntryPrice,
            StopLoss:         t.StopLoss,
            TakeProfit:       t.TakeProfit,
            Status:           t.StatusEnum,
            CreatedAt:        t.CreatedAt.ToUnixTimeMilliseconds(),
            OpenedAt:         t.OpenedAt?.ToUnixTimeMilliseconds(),
            ClosedAt:         t.ClosedAt?.ToUnixTimeMilliseconds(),
            ClosedPrice:      t.ClosedPrice,
            CloseReason:      t.CloseReasonEnum,
            Fee:              t.Fee,
            Pnl:              t.Pnl,
            AccountPnl:       accountPnl,
            UnrealizedPnl:    null,
            TradingAccountId: t.TradingAccountId,
            BacktestId:       t.BacktestId);
}
