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

        var tradeBot = await dbContext.TradeBots
            .Include(b => b.TradingAccount)
            .Where(b => b.IsEnabled && b.TradingAccount.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No active tradebot found. Enable a tradebot before creating a backtest.");

        var strategy = await dbContext.TradingStrategies
            .FirstOrDefaultAsync(s => s.Id == tradeBot.TradingAccount.TradingStrategyId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Strategy {tradeBot.TradingAccount.TradingStrategyId} not found.");

        var now = timeProvider.GetUtcNow();
        var backtest = new Backtest
        {
            SymbolId          = symbol.Id,
            IntervalId        = interval.Id,
            TradingStrategyId = tradeBot.TradingAccount.TradingStrategyId,
            TradeBotId        = tradeBot.Id,
            Quantity          = tradeBot.Quantity,
            StopLoss          = tradeBot.StopLoss,
            TakeProfit        = tradeBot.TakeProfit,
            From              = request.From,
            To                = request.To,
            StartedAt         = now,
            Status            = BacktestStatus.Pending,
            InitialBalance    = request.InitialBalance,
            CandleCount       = 0
        };

        dbContext.Backtests.Add(backtest);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummaryDto(backtest, symbol, interval, strategy, 0);
    }

    public async Task<IReadOnlyList<BacktestSummaryResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var backtests = await dbContext.Backtests
            .AsNoTracking()
            .Include(b => b.Symbol)
            .Include(b => b.Interval)
            .Include(b => b.TradingStrategy)
            .Include(b => b.Trades)
            .OrderByDescending(b => b.StartedAt)
            .ToListAsync(cancellationToken);

        return backtests
            .Select(b => ToSummaryDto(b, b.Symbol, b.Interval, b.TradingStrategy, b.Trades.Count))
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
            .Include(b => b.TradingStrategy)
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
            .Select(TradeToDto)
            .ToList();

        var equity = BuildEquityCurve(backtest);

        return new BacktestDetailResponseDto(
            Id:             backtest.Id,
            TradeBotId:     backtest.TradeBotId,
            SymbolCode:     backtest.Symbol.Code,
            IntervalCode:   backtest.Interval.Code,
            StrategyName:   backtest.TradingStrategy.Name,
            From:           backtest.From.ToUnixTimeSeconds(),
            To:             backtest.To.ToUnixTimeSeconds(),
            StartedAt:      backtest.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    backtest.CompletedAt?.ToUnixTimeMilliseconds(),
            Status:         backtest.Status,
            InitialBalance: backtest.InitialBalance,
            FinalBalance:   backtest.FinalBalance,
            Pnl:            backtest.Pnl,
            Quantity:       backtest.Quantity,
            StopLoss:       backtest.StopLoss,
            TakeProfit:     backtest.TakeProfit,
            CandleCount:    backtest.CandleCount,
            Trades:         tradeDtos,
            Candles:        candles,
            EquityCurve:    equity);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static IReadOnlyList<EquityPointDto> BuildEquityCurve(Backtest backtest)
    {
        var points = new List<EquityPointDto>
        {
            new(backtest.From.ToUnixTimeSeconds(), backtest.InitialBalance)
        };

        var balance = backtest.InitialBalance;

        foreach (var trade in backtest.Trades.Where(t => t.Pnl.HasValue).OrderBy(t => t.ClosedAt))
        {
            balance += trade.Pnl!.Value;
            points.Add(new EquityPointDto(
                trade.ClosedAt!.Value.ToUnixTimeSeconds(),
                balance));
        }

        return points;
    }

    private static BacktestSummaryResponseDto ToSummaryDto(
        Backtest b,
        Symbol symbol,
        Interval interval,
        Models.Lookups.TradingStrategy strategy,
        int tradeCount) =>
        new(
            Id:             b.Id,
            TradeBotId:     b.TradeBotId,
            SymbolCode:     symbol.Code,
            IntervalCode:   interval.Code,
            StrategyName:   strategy.Name,
            From:           b.From.ToUnixTimeSeconds(),
            To:             b.To.ToUnixTimeSeconds(),
            StartedAt:      b.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt:    b.CompletedAt?.ToUnixTimeMilliseconds(),
            Status:         b.Status,
            InitialBalance: b.InitialBalance,
            FinalBalance:   b.FinalBalance,
            Pnl:            b.Pnl,
            Quantity:       b.Quantity,
            StopLoss:       b.StopLoss,
            TakeProfit:     b.TakeProfit,
            CandleCount:    b.CandleCount,
            TradeCount:     tradeCount);

    private static TradeResponseDto TradeToDto(Trade t) =>
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
            Pnl:              t.Pnl,
            UnrealizedPnl:    null,
            TradingAccountId: t.TradingAccountId);
}
