using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.PriceFeeds;

namespace TraderAlgoApi.Services.Trades;

public sealed class TradeService(
    ApplicationDbContext dbContext,
    PriceFeed priceFeed,
    TimeProvider timeProvider,
    ILogger<TradeService> logger) : ITradeService
{
    public async Task<TradeResponseDto> CreateAsync(
        CreateTradeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.OrderType == TradeOrderType.Limit && request.LimitPrice is null)
            throw new ArgumentException("LimitPrice is required for limit orders.");

        var hasOpen = await dbContext.Trades.AnyAsync(
            t => t.SymbolCode == request.SymbolCode &&
                 (t.StatusId == (int)TradeStatus.Pending || t.StatusId == (int)TradeStatus.Active),
            cancellationToken);

        if (hasOpen)
            throw new InvalidOperationException(
                $"A pending or active trade already exists for {request.SymbolCode}. Close it before opening a new one.");

        var now = timeProvider.GetUtcNow();
        Trade trade;

        if (request.OrderType == TradeOrderType.Market)
        {
            var price = await ResolveCurrentPriceAsync(request.SymbolCode, cancellationToken);

            trade = new Trade
            {
                SymbolCode     = request.SymbolCode,
                IntervalCode   = request.IntervalCode,
                Side           = request.Side,
                OrderType      = TradeOrderType.Market,
                Quantity       = request.Quantity,
                RequestedPrice = null,
                EntryPrice     = price,
                StopLoss       = request.StopLoss,
                TakeProfit     = request.TakeProfit,
                Status         = TradeStatus.Active,
                CreatedAt      = now,
                OpenedAt       = now
            };
        }
        else
        {
            trade = new Trade
            {
                SymbolCode     = request.SymbolCode,
                IntervalCode   = request.IntervalCode,
                Side           = request.Side,
                OrderType      = TradeOrderType.Limit,
                Quantity       = request.Quantity,
                RequestedPrice = request.LimitPrice,
                EntryPrice     = null,
                StopLoss       = request.StopLoss,
                TakeProfit     = request.TakeProfit,
                Status         = TradeStatus.Pending,
                CreatedAt      = now
            };
        }

        dbContext.Trades.Add(trade);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Trade {Id} created: {Symbol} {Side} {OrderType} qty={Quantity}",
            trade.Id, trade.SymbolCode, trade.Side, trade.OrderType, trade.Quantity);

        return ToDto(trade);
    }

    public async Task<TradeResponseDto> StopAsync(long id, CancellationToken cancellationToken = default)
    {
        var trade = await dbContext.Trades.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"Trade {id} not found.");

        if (trade.Status != TradeStatus.Active)
            throw new InvalidOperationException(
                $"Trade {id} cannot be stopped: current status is {trade.Status}.");

        var price = await ResolveCurrentPriceAsync(trade.SymbolCode, cancellationToken);
        var now   = timeProvider.GetUtcNow();

        trade.Status      = TradeStatus.Closed;
        trade.ClosedAt    = now;
        trade.ClosedPrice = price;
        trade.CloseReason = TradeCloseReason.Manual;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Trade {Id} closed manually at {Price}", id, price);

        return ToDto(trade);
    }

    public async Task<TradeResponseDto> UpdateAsync(
        long id,
        UpdateTradeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var trade = await dbContext.Trades.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"Trade {id} not found.");

        if (trade.Status is not (TradeStatus.Active or TradeStatus.Pending))
            throw new InvalidOperationException(
                $"Trade {id} cannot be updated: current status is {trade.Status}.");

        trade.StopLoss   = request.StopLoss;
        trade.TakeProfit = request.TakeProfit;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(trade);
    }

    public async Task<IReadOnlyList<TradeResponseDto>> GetActiveAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var trades = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.SymbolCode == symbol &&
                        (t.StatusId == (int)TradeStatus.Active || t.StatusId == (int)TradeStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return trades.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TradeResponseDto>> GetHistoryAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var trades = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.SymbolCode == symbol &&
                        (t.StatusId == (int)TradeStatus.Closed || t.StatusId == (int)TradeStatus.Cancelled))
            .OrderByDescending(t => t.ClosedAt)
            .ToListAsync(cancellationToken);

        return trades.Select(ToDto).ToList();
    }

    public async Task EvaluatePriceAsync(
        string symbol,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        var trades = await dbContext.Trades
            .Where(t => t.SymbolCode == symbol &&
                        (t.StatusId == (int)TradeStatus.Pending || t.StatusId == (int)TradeStatus.Active))
            .ToListAsync(cancellationToken);

        if (trades.Count == 0)
            return;

        var now     = timeProvider.GetUtcNow();
        var changed = false;

        foreach (var trade in trades)
        {
            changed |= trade.Status == TradeStatus.Pending
                ? TryFillLimit(trade, price, now)
                : TryTriggerSLTP(trade, price, now);
        }

        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool TryFillLimit(Trade trade, decimal price, DateTimeOffset now)
    {
        var limit = trade.RequestedPrice!.Value;

        var fills = trade.Side == TradeSide.Buy
            ? price <= limit   // buy limit: fill when market drops to or below limit
            : price >= limit;  // sell limit: fill when market rises to or above limit

        if (!fills)
            return false;

        trade.Status     = TradeStatus.Active;
        trade.EntryPrice = limit;
        trade.OpenedAt   = now;
        return true;
    }

    private static bool TryTriggerSLTP(Trade trade, decimal price, DateTimeOffset now)
    {
        TradeCloseReason? reason = null;

        if (trade.StopLoss.HasValue)
        {
            var hit = trade.Side == TradeSide.Buy
                ? price <= trade.StopLoss.Value   // long: SL is below entry
                : price >= trade.StopLoss.Value;  // short: SL is above entry
            if (hit)
                reason = TradeCloseReason.StopLoss;
        }

        if (reason is null && trade.TakeProfit.HasValue)
        {
            var hit = trade.Side == TradeSide.Buy
                ? price >= trade.TakeProfit.Value   // long: TP is above entry
                : price <= trade.TakeProfit.Value;  // short: TP is below entry
            if (hit)
                reason = TradeCloseReason.TakeProfit;
        }

        if (reason is null)
            return false;

        trade.Status      = TradeStatus.Closed;
        trade.ClosedAt    = now;
        trade.ClosedPrice = price;
        trade.CloseReason = reason;
        return true;
    }

    private async Task<decimal> ResolveCurrentPriceAsync(string symbol, CancellationToken cancellationToken)
    {
        var live = priceFeed.GetLatestPrice(symbol);
        if (live.HasValue)
            return live.Value;

        var stored = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.Symbol.Code == symbol)
            .OrderByDescending(k => k.OpenTime)
            .Select(k => (decimal?)k.Close)
            .FirstOrDefaultAsync(cancellationToken);

        return stored ?? throw new InvalidOperationException(
            $"No price available for symbol {symbol}. Ensure the market data stream is running.");
    }

    private static TradeResponseDto ToDto(Trade t) => new(
        Id:             t.Id,
        SymbolCode:     t.SymbolCode,
        IntervalCode:   t.IntervalCode,
        Side:           t.Side,
        OrderType:      t.OrderType,
        Quantity:       t.Quantity,
        RequestedPrice: t.RequestedPrice,
        EntryPrice:     t.EntryPrice,
        StopLoss:       t.StopLoss,
        TakeProfit:     t.TakeProfit,
        Status:         t.Status,
        CreatedAt:      t.CreatedAt.ToUnixTimeMilliseconds(),
        OpenedAt:       t.OpenedAt?.ToUnixTimeMilliseconds(),
        ClosedAt:       t.ClosedAt?.ToUnixTimeMilliseconds(),
        ClosedPrice:    t.ClosedPrice,
        CloseReason:    t.CloseReason);
}
