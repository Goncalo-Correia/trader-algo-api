using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Backtests;
using TraderAlgoApi.Dtos.Charts;
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
        var quantity = request.Quantity ?? templateBot.Quantity;
        var stopLoss = request.StopLoss ?? templateBot.StopLoss;
        var takeProfit = request.TakeProfit ?? templateBot.TakeProfit;

        if (quantity <= 0)
            throw new ArgumentException("'quantity' must be greater than zero.");

        var strategy = await dbContext.TradingStrategies
            .FirstOrDefaultAsync(s => s.Id == tradingStrategyId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Strategy {tradingStrategyId} not found.");

        var now = timeProvider.GetUtcNow();
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
            SymbolId           = symbol.Id,
            IntervalId         = interval.Id,
            IsEnabled          = true,
            Quantity           = quantity,
            StopLoss           = stopLoss,
            TakeProfit         = takeProfit,
            Breakeven          = request.Breakeven,
            BreakevenStop      = request.BreakevenStop,
            Fee                = request.Fee,
            IsNySessionOnly    = request.IsNySessionOnly,
            DailyProfitGoal    = request.DailyProfitGoal,
            MaxLossesPerDay    = request.MaxLossesPerDay,
            MaxCandlesPerTrade = request.MaxCandlesPerTrade,
            CreatedAt          = now,
            UpdatedAt          = now
        };

        dbContext.TradeBots.Add(tradeBot);
        await dbContext.SaveChangesAsync(cancellationToken);

        backtest.TradeBotId = tradeBot.Id;
        backtest.TradeBot = tradeBot;
        tradeBot.TradingStrategy = strategy;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummaryDto(backtest, symbol, interval, 0);
    }

    public async Task<IReadOnlyList<BacktestSummaryResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var backtests = await dbContext.Backtests
            .AsNoTracking()
            .Include(b => b.Symbol)
            .Include(b => b.Interval)
            .Include(b => b.TradeBot)
                .ThenInclude(tb => tb!.TradingStrategy)
            .Include(b => b.Trades)
            .OrderByDescending(b => b.StartedAt)
            .ToListAsync(cancellationToken);

        return backtests
            .Select(b => ToSummaryDto(b, b.Symbol, b.Interval, b.Trades.Count))
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

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == backtest.SymbolId &&
                        k.IntervalId == backtest.IntervalId &&
                        k.OpenTime >= backtest.From &&
                        k.OpenTime <= backtest.To)
            .Include(k => k.SimpleMovingAverage)
            .Include(k => k.RelativeStrengthIndex)
            .Include(k => k.Macd)
            .OrderBy(k => k.OpenTime)
            .Select(k => new CandleWithIndicatorsResponseDto(
                k.OpenTime.ToUnixTimeSeconds(),
                k.Open, k.High, k.Low, k.Close, k.Volume,
                k.TakerBuyBaseAssetVolume,
                k.Volume - k.TakerBuyBaseAssetVolume,
                k.SimpleMovingAverage!.Sma20,
                k.SimpleMovingAverage!.Sma100,
                k.RelativeStrengthIndex!.Rsi,
                k.RelativeStrengthIndex!.RsiSmooth,
                k.RelativeStrengthIndex!.Divergence,
                k.Macd!.MacdLine,
                k.Macd!.SignalLine,
                k.Macd!.Histogram))
            .ToListAsync(cancellationToken);

        var tradeDtos = backtest.Trades
            .OrderBy(t => t.OpenedAt)
            .Select(t => TradeToDto(t, t.AccountPnl))
            .ToList();

        var equity = BuildEquityCurve(backtest);
        var (maxDrawdown, maxTrailingDrawdown) = ComputeDrawdowns(equity, backtest.InitialBalance);

        return new BacktestDetailResponseDto(
            Id:             backtest.Id,
            TradeBotId:     backtest.TradeBotId,
            SymbolCode:     backtest.Symbol.Code,
            IntervalCode:   backtest.Interval.Code,
            StrategyName:   backtest.TradeBot?.TradingStrategy?.Name ?? string.Empty,
            From:           backtest.From.ToUnixTimeSeconds(),
            To:             backtest.To.ToUnixTimeSeconds(),
            StartedAt:      backtest.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    backtest.CompletedAt?.ToUnixTimeMilliseconds(),
            Status:         (BacktestStatus)backtest.StatusId,
            InitialBalance: backtest.InitialBalance,
            FinalBalance:   backtest.FinalBalance,
            Pnl:            backtest.Pnl,
            Quantity:       backtest.TradeBot?.Quantity ?? 0,
            StopLoss:       backtest.TradeBot?.StopLoss,
            TakeProfit:     backtest.TradeBot?.TakeProfit,
            Breakeven:      backtest.TradeBot?.Breakeven,
            BreakevenStop:  backtest.TradeBot?.BreakevenStop,
            IsNySessionOnly: backtest.TradeBot?.IsNySessionOnly ?? false,
            Delay:          backtest.TradeBot?.Delay ?? false,
            DailyProfitGoal: backtest.TradeBot?.DailyProfitGoal,
            MaxLossesPerDay: backtest.TradeBot?.MaxLossesPerDay,
            MaxCandlesPerTrade: backtest.TradeBot?.MaxCandlesPerTrade,
            CandleCount:    backtest.CandleCount,
            MaxDrawdown:    maxDrawdown,
            MaxTrailingDrawdown: maxTrailingDrawdown,
            Trades:         tradeDtos,
            Candles:        candles,
            EquityCurve:    equity);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Backtests
            .AnyAsync(b => b.Id == id, cancellationToken);

        if (!exists)
            throw new KeyNotFoundException($"Backtest {id} not found.");

        await dbContext.Trades
            .Where(t => t.BacktestId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.TradeBots
            .Where(b => b.BacktestId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Backtests
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<TradeBot> LoadTemplateBotAsync(
        CreateBacktestRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.TradingStrategyId.HasValue && request.Quantity.HasValue)
        {
            return new TradeBot
            {
                TradingStrategyId = request.TradingStrategyId.Value,
                Quantity = request.Quantity.Value,
                StopLoss = request.StopLoss,
                TakeProfit = request.TakeProfit
            };
        }

        return await dbContext.TradeBots
            .Include(b => b.TradingAccount)
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

    private static IReadOnlyList<EquityPointDto> BuildEquityCurve(Backtest backtest)
    {
        var points = new List<EquityPointDto>
        {
            new(backtest.From.ToUnixTimeSeconds(), backtest.InitialBalance, null)
        };

        var balance = backtest.InitialBalance;

        foreach (var trade in backtest.Trades.Where(t => t.Pnl.HasValue).OrderBy(t => t.ClosedAt))
        {
            balance += trade.Pnl!.Value;
            points.Add(new EquityPointDto(
                trade.ClosedAt!.Value.ToUnixTimeSeconds(),
                balance,
                trade.Pnl));
        }

        return points;
    }

    /// <summary>
    /// Returns (maxDrawdown, maxTrailingDrawdown) from the equity curve.
    /// maxDrawdown        — largest absolute dollar amount the balance dropped below the initial balance.
    /// maxTrailingDrawdown — largest absolute dollar drop from any peak to any subsequent balance.
    /// Both are null when there are no closed trades.
    /// </summary>
    private static (decimal? MaxDrawdown, decimal? MaxTrailingDrawdown) ComputeDrawdowns(
        IReadOnlyList<EquityPointDto> equity,
        decimal initialBalance)
    {
        if (equity.Count <= 1)
            return (null, null);

        var peak   = initialBalance;
        var maxDD  = 0m;
        var maxTDD = 0m;

        foreach (var point in equity)
        {
            var belowStart = initialBalance - point.Balance;
            if (belowStart > maxDD)
                maxDD = belowStart;

            if (point.Balance > peak)
                peak = point.Balance;

            var dropFromPeak = peak - point.Balance;
            if (dropFromPeak > maxTDD)
                maxTDD = dropFromPeak;
        }

        return (maxDD > 0 ? maxDD : null, maxTDD > 0 ? maxTDD : null);
    }

    private static BacktestSummaryResponseDto ToSummaryDto(
        Backtest b,
        Symbol symbol,
        Interval interval,
        int tradeCount)
    {
        var equity = BuildEquityCurve(b);
        var (maxDrawdown, maxTrailingDrawdown) = ComputeDrawdowns(equity, b.InitialBalance);

        return new(
            Id:             b.Id,
            TradeBotId:     b.TradeBotId,
            SymbolCode:     symbol.Code,
            IntervalCode:   interval.Code,
            StrategyName:   b.TradeBot?.TradingStrategy?.Name ?? string.Empty,
            From:           b.From.ToUnixTimeSeconds(),
            To:             b.To.ToUnixTimeSeconds(),
            StartedAt:      b.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    b.CompletedAt?.ToUnixTimeMilliseconds(),
            Status:         (BacktestStatus)b.StatusId,
            InitialBalance: b.InitialBalance,
            FinalBalance:   b.FinalBalance,
            Pnl:            b.Pnl,
            Quantity:       b.TradeBot?.Quantity ?? 0,
            StopLoss:       b.TradeBot?.StopLoss,
            TakeProfit:     b.TradeBot?.TakeProfit,
            Breakeven:      b.TradeBot?.Breakeven,
            BreakevenStop:  b.TradeBot?.BreakevenStop,
            IsNySessionOnly: b.TradeBot?.IsNySessionOnly ?? false,
            Delay:          b.TradeBot?.Delay ?? false,
            DailyProfitGoal: b.TradeBot?.DailyProfitGoal,
            MaxLossesPerDay: b.TradeBot?.MaxLossesPerDay,
            MaxCandlesPerTrade: b.TradeBot?.MaxCandlesPerTrade,
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
            Side:             (TradeSide)t.SideId,
            OrderType:        (TradeOrderType)t.OrderTypeId,
            Quantity:         t.Quantity,
            RequestedPrice:   t.RequestedPrice,
            EntryPrice:       t.EntryPrice,
            StopLoss:         t.StopLoss,
            TakeProfit:       t.TakeProfit,
            Status:           (TradeStatus)t.StatusId,
            CreatedAt:        t.CreatedAt.ToUnixTimeMilliseconds(),
            OpenedAt:         t.OpenedAt?.ToUnixTimeMilliseconds(),
            ClosedAt:         t.ClosedAt?.ToUnixTimeMilliseconds(),
            ClosedPrice:      t.ClosedPrice,
            CloseReason:      t.CloseReasonId is int id ? (TradeCloseReason)id : null,
            Fee:              t.Fee,
            Pnl:              t.Pnl,
            AccountPnl:       accountPnl,
            UnrealizedPnl:    null,
            TradingAccountId: t.TradingAccountId,
            BacktestId:       t.BacktestId);
}
