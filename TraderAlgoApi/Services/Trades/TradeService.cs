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

        var symbol = await dbContext.Symbols
            .FirstOrDefaultAsync(s => s.Code == request.SymbolCode, cancellationToken)
            ?? throw new ArgumentException($"Symbol '{request.SymbolCode}' not found.");

        int? intervalId = null;
        if (request.IntervalCode is not null)
        {
            var interval = await dbContext.Intervals
                .FirstOrDefaultAsync(i => i.Code == request.IntervalCode, cancellationToken)
                ?? throw new ArgumentException($"Interval '{request.IntervalCode}' not found.");
            intervalId = interval.Id;
        }

        var hasOpen = await dbContext.Trades.AnyAsync(
            t => t.SymbolId == symbol.Id &&
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
                SymbolId       = symbol.Id,
                IntervalId     = intervalId,
                SideId         = (int)request.Side,
                OrderTypeId    = (int)TradeOrderType.Market,
                Quantity       = request.Quantity,
                RequestedPrice = null,
                EntryPrice     = price,
                StopLoss       = request.StopLoss,
                TakeProfit     = request.TakeProfit,
                StatusId       = (int)TradeStatus.Active,
                CreatedAt      = now,
                OpenedAt       = now
            };
        }
        else
        {
            trade = new Trade
            {
                SymbolId       = symbol.Id,
                IntervalId     = intervalId,
                SideId         = (int)request.Side,
                OrderTypeId    = (int)TradeOrderType.Limit,
                Quantity       = request.Quantity,
                RequestedPrice = request.LimitPrice,
                EntryPrice     = null,
                StopLoss       = request.StopLoss,
                TakeProfit     = request.TakeProfit,
                StatusId       = (int)TradeStatus.Pending,
                CreatedAt      = now
            };
        }

        dbContext.Trades.Add(trade);
        await dbContext.SaveChangesAsync(cancellationToken);

        // EF's change tracker has symbol (and interval if set) in scope, so
        // relationship fixup populates the navigation properties after save.
        logger.LogInformation(
            "Trade {Id} created: {Symbol} {Side} {OrderType} qty={Quantity}",
            trade.Id, symbol.Code, (TradeSide)trade.SideId, (TradeOrderType)trade.OrderTypeId, trade.Quantity);

        return ToDto(trade);
    }

    public async Task<TradeResponseDto> StopAsync(long id, CancellationToken cancellationToken = default)
    {
        var trade = await TradeWithNavigations()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Trade {id} not found.");

        if (trade.StatusId != (int)TradeStatus.Active)
            throw new InvalidOperationException(
                $"Trade {id} cannot be stopped: current status is {(TradeStatus)trade.StatusId}.");

        var price = await ResolveCurrentPriceAsync(trade.Symbol.Code, cancellationToken);
        var now   = timeProvider.GetUtcNow();

        trade.StatusId      = (int)TradeStatus.Closed;
        trade.ClosedAt      = now;
        trade.ClosedPrice   = price;
        trade.CloseReasonId = (int)TradeCloseReason.Manual;
        trade.Pnl           = CalculatePnl(trade, price);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Trade {Id} closed manually at {Price} pnl={Pnl}", id, price, trade.Pnl);

        return ToDto(trade);
    }

    public async Task<TradeResponseDto> UpdateAsync(
        long id,
        UpdateTradeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var trade = await TradeWithNavigations()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Trade {id} not found.");

        if (trade.StatusId != (int)TradeStatus.Active && trade.StatusId != (int)TradeStatus.Pending)
            throw new InvalidOperationException(
                $"Trade {id} cannot be updated: current status is {(TradeStatus)trade.StatusId}.");

        trade.StopLoss   = request.StopLoss;
        trade.TakeProfit = request.TakeProfit;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(trade);
    }

    public async Task<IReadOnlyList<TradeResponseDto>> GetActiveAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var trades = await TradeWithNavigations()
            .AsNoTracking()
            .Where(t => t.Symbol.Code == symbol &&
                        (t.StatusId == (int)TradeStatus.Active || t.StatusId == (int)TradeStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return trades.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TradeResponseDto>> GetHistoryAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var trades = await TradeWithNavigations()
            .AsNoTracking()
            .Where(t => t.Symbol.Code == symbol &&
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
        // No Include needed — ToDto is not called here.
        var trades = await dbContext.Trades
            .Where(t => t.Symbol.Code == symbol &&
                        (t.StatusId == (int)TradeStatus.Pending || t.StatusId == (int)TradeStatus.Active))
            .ToListAsync(cancellationToken);

        if (trades.Count == 0)
            return;

        var now     = timeProvider.GetUtcNow();
        var changed = false;

        foreach (var trade in trades)
        {
            changed |= trade.StatusId == (int)TradeStatus.Pending
                ? TryFillLimit(trade, price, now)
                : TryTriggerSLTP(trade, price, now);
        }

        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Base query that includes all navigation properties needed by ToDto.</summary>
    private IQueryable<Trade> TradeWithNavigations() =>
        dbContext.Trades
            .Include(t => t.Symbol)
            .Include(t => t.Interval);

    private bool TryFillLimit(Trade trade, decimal price, DateTimeOffset now)
    {
        var limit = trade.RequestedPrice!.Value;

        var fills = trade.SideId == (int)TradeSide.Buy
            ? price <= limit   // buy limit: fill when market drops to or below limit
            : price >= limit;  // sell limit: fill when market rises to or above limit

        logger.LogDebug(
            "Trade {Id} limit check: side={Side} limit={Limit} price={Price} fills={Fills}",
            trade.Id, (TradeSide)trade.SideId, limit, price, fills);

        if (!fills)
            return false;

        trade.StatusId   = (int)TradeStatus.Active;
        trade.EntryPrice = limit;
        trade.OpenedAt   = now;
        return true;
    }

    private bool TryTriggerSLTP(Trade trade, decimal price, DateTimeOffset now)
    {
        var entry = trade.EntryPrice!.Value;

        // StopLoss and TakeProfit are stored as positive unit offsets from entry.
        // Buy (long):  SL triggers below entry,  TP triggers above entry.
        // Sell (short): SL triggers above entry, TP triggers below entry.
        var isBuy = trade.SideId == (int)TradeSide.Buy;

        TradeCloseReason? reason = null;

        if (trade.StopLoss.HasValue)
        {
            var slPrice = isBuy ? entry - trade.StopLoss.Value : entry + trade.StopLoss.Value;
            var hit     = isBuy ? price <= slPrice             : price >= slPrice;

            logger.LogDebug(
                "Trade {Id} SL check: side={Side} entry={Entry} slOffset={SlOffset} slPrice={SlPrice} price={Price} hit={Hit}",
                trade.Id, (TradeSide)trade.SideId, entry, trade.StopLoss.Value, slPrice, price, hit);

            if (hit)
                reason = TradeCloseReason.StopLoss;
        }

        if (reason is null && trade.TakeProfit.HasValue)
        {
            var tpPrice = isBuy ? entry + trade.TakeProfit.Value : entry - trade.TakeProfit.Value;
            var hit     = isBuy ? price >= tpPrice               : price <= tpPrice;

            logger.LogDebug(
                "Trade {Id} TP check: side={Side} entry={Entry} tpOffset={TpOffset} tpPrice={TpPrice} price={Price} hit={Hit}",
                trade.Id, (TradeSide)trade.SideId, entry, trade.TakeProfit.Value, tpPrice, price, hit);

            if (hit)
                reason = TradeCloseReason.TakeProfit;
        }

        if (reason is null)
            return false;

        logger.LogInformation(
            "Trade {Id} triggered {Reason}: entry={Entry} closedPrice={Price} slOffset={SlOffset} tpOffset={TpOffset}",
            trade.Id, reason.Value, entry, price, trade.StopLoss, trade.TakeProfit);

        trade.StatusId      = (int)TradeStatus.Closed;
        trade.ClosedAt      = now;
        trade.ClosedPrice   = price;
        trade.CloseReasonId = (int)reason.Value;
        trade.Pnl           = CalculatePnl(trade, price);
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

    private TradeResponseDto ToDto(Trade t)
    {
        decimal? unrealizedPnl = null;
        if (t.StatusId == (int)TradeStatus.Active && t.EntryPrice.HasValue)
        {
            var currentPrice = priceFeed.GetLatestPrice(t.Symbol.Code);
            if (currentPrice.HasValue)
            {
                unrealizedPnl = t.SideId == (int)TradeSide.Buy
                    ? (currentPrice.Value - t.EntryPrice.Value) * t.Quantity
                    : (t.EntryPrice.Value - currentPrice.Value) * t.Quantity;
            }
        }

        return new TradeResponseDto(
            Id:             t.Id,
            SymbolCode:     t.Symbol.Code,
            IntervalCode:   t.Interval?.Code,
            Side:           (TradeSide)t.SideId,
            OrderType:      (TradeOrderType)t.OrderTypeId,
            Quantity:       t.Quantity,
            RequestedPrice: t.RequestedPrice,
            EntryPrice:     t.EntryPrice,
            StopLoss:       t.StopLoss,
            TakeProfit:     t.TakeProfit,
            Status:         (TradeStatus)t.StatusId,
            CreatedAt:      t.CreatedAt.ToUnixTimeMilliseconds(),
            OpenedAt:       t.OpenedAt?.ToUnixTimeMilliseconds(),
            ClosedAt:       t.ClosedAt?.ToUnixTimeMilliseconds(),
            ClosedPrice:    t.ClosedPrice,
            CloseReason:    t.CloseReasonId is int id ? (TradeCloseReason)id : null,
            Pnl:            t.Pnl,
            UnrealizedPnl:  unrealizedPnl);
    }

    private static decimal? CalculatePnl(Trade trade, decimal closedPrice)
    {
        if (trade.EntryPrice is null)
            return null;

        return trade.SideId == (int)TradeSide.Buy
            ? (closedPrice - trade.EntryPrice.Value) * trade.Quantity
            : (trade.EntryPrice.Value - closedPrice) * trade.Quantity;
    }
}
