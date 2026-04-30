using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Trades;
using TraderAlgoApi.Dtos.TradeEvents;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.PriceFeeds;
using TraderAlgoApi.Services.TradeEvents;

namespace TraderAlgoApi.Services.Trades;

public sealed class TradeService(
    ApplicationDbContext dbContext,
    PriceFeed priceFeed,
    TimeProvider timeProvider,
    ITradeEventPublisher tradeEventPublisher,
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
        Interval? intervalEntity = null;
        if (request.IntervalCode is not null)
        {
            intervalEntity = await dbContext.Intervals
                .FirstOrDefaultAsync(i => i.Code == request.IntervalCode, cancellationToken)
                ?? throw new ArgumentException($"Interval '{request.IntervalCode}' not found.");
            intervalId = intervalEntity.Id;
        }

        if (request.TradingAccountId is long requestedAccountId)
        {
            var account = await dbContext.TradingAccounts
                .FirstOrDefaultAsync(a => a.Id == requestedAccountId, cancellationToken)
                ?? throw new ArgumentException($"Trading account {requestedAccountId} not found.");

            if (!account.IsActive)
                throw new ArgumentException($"Trading account {requestedAccountId} is not active.");
        }

        var openTrades = dbContext.Trades
            .Where(t => t.StatusId == (int)TradeStatus.Pending || t.StatusId == (int)TradeStatus.Active);

        var hasOpen = request.TradingAccountId is long openAccountId
            ? await openTrades.AnyAsync(t => t.TradingAccountId == openAccountId, cancellationToken)
            : await openTrades.AnyAsync(t => t.SymbolId == symbol.Id, cancellationToken);

        if (hasOpen)
            throw new InvalidOperationException(
                request.TradingAccountId is long blockedAccountId
                    ? $"A pending or active trade already exists for trading account {blockedAccountId}. Close it before opening a new one."
                    : $"A pending or active trade already exists for {request.SymbolCode}. Close it before opening a new one.");

        var now = timeProvider.GetUtcNow();
        Trade trade;

        if (request.OrderType == TradeOrderType.Market)
        {
            var price = await ResolveCurrentPriceAsync(request.SymbolCode, cancellationToken);

            trade = new Trade
            {
                SymbolId         = symbol.Id,
                Symbol           = symbol,
                IntervalId       = intervalId,
                Interval         = intervalEntity,
                SideId           = (int)request.Side,
                OrderTypeId      = (int)TradeOrderType.Market,
                Quantity         = request.Quantity,
                RequestedPrice   = null,
                EntryPrice       = price,
                StopLoss         = request.StopLoss,
                TakeProfit       = request.TakeProfit,
                StatusId         = (int)TradeStatus.Active,
                CreatedAt        = now,
                OpenedAt         = now,
                TradingAccountId = request.TradingAccountId
            };
        }
        else
        {
            trade = new Trade
            {
                SymbolId         = symbol.Id,
                Symbol           = symbol,
                IntervalId       = intervalId,
                Interval         = intervalEntity,
                SideId           = (int)request.Side,
                OrderTypeId      = (int)TradeOrderType.Limit,
                Quantity         = request.Quantity,
                RequestedPrice   = request.LimitPrice,
                EntryPrice       = null,
                StopLoss         = request.StopLoss,
                TakeProfit       = request.TakeProfit,
                StatusId         = (int)TradeStatus.Pending,
                CreatedAt        = now,
                TradingAccountId = request.TradingAccountId
            };
        }

        dbContext.Trades.Add(trade);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Trade {Id} created: {Symbol} {Side} {OrderType} qty={Quantity} accountId={AccountId}",
            trade.Id, symbol.Code, (TradeSide)trade.SideId, (TradeOrderType)trade.OrderTypeId,
            trade.Quantity, trade.TradingAccountId);

        var dto = ToDto(trade);
        PublishTradeEvent(
            dto.Status == TradeStatus.Active ? "TradeOpened" : "TradePending",
            dto,
            dto.Status == TradeStatus.Active ? "Trade opened." : "Trade pending.");

        return dto;
    }

    public async Task<TradeResponseDto> StopAsync(long id, CancellationToken cancellationToken = default)
    {
        return await CloseAsync(id, TradeCloseReason.Manual, cancellationToken);
    }

    public async Task<TradeResponseDto> CloseAsync(
        long id,
        TradeCloseReason closeReason,
        CancellationToken cancellationToken = default)
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
        trade.CloseReasonId = (int)closeReason;
        trade.Pnl           = CalculatePnl(trade, price);

        await ApplyPnlToAccountAsync(trade, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Trade {Id} closed with {Reason} at {Price} pnl={Pnl}", id, closeReason, price, trade.Pnl);

        var dto = ToDto(trade);
        PublishTradeEvent("TradeClosed", dto, closeReason == TradeCloseReason.BotSignal
            ? "Trade closed by tradebot signal."
            : "Trade closed manually.");

        return dto;
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
        long tradingAccountId,
        CancellationToken cancellationToken = default)
    {
        var trades = await TradeWithNavigations()
            .AsNoTracking()
            .Where(t => t.TradingAccountId == tradingAccountId &&
                        (t.StatusId == (int)TradeStatus.Active || t.StatusId == (int)TradeStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return trades.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TradeResponseDto>> GetHistoryAsync(
        long tradingAccountId,
        CancellationToken cancellationToken = default)
    {
        var trades = await TradeWithNavigations()
            .AsNoTracking()
            .Where(t => t.TradingAccountId == tradingAccountId &&
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
        var tradeEvents = new List<(long TradeId, string Type, string Message)>();

        foreach (var trade in trades)
        {
            var previousStatusId = trade.StatusId;
            var wasChanged = previousStatusId == (int)TradeStatus.Pending
                ? TryFillLimit(trade, price, now)
                : TryTriggerSLTP(trade, price, now);

            if (wasChanged && trade.StatusId == (int)TradeStatus.Closed)
            {
                await ApplyPnlToAccountAsync(trade, cancellationToken);
                tradeEvents.Add((trade.Id, "TradeClosed", "Trade closed by price trigger."));
            }

            if (wasChanged &&
                previousStatusId == (int)TradeStatus.Pending &&
                trade.StatusId == (int)TradeStatus.Active)
            {
                tradeEvents.Add((trade.Id, "TradeOpened", "Pending trade filled."));
            }

            changed |= wasChanged;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var changedIds = tradeEvents.Select(e => e.TradeId).Distinct().ToList();
            var changedTrades = await TradeWithNavigations()
                .AsNoTracking()
                .Where(t => changedIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken);

            foreach (var tradeEvent in tradeEvents)
            {
                if (!changedTrades.TryGetValue(tradeEvent.TradeId, out var trade))
                    continue;

                PublishTradeEvent(tradeEvent.Type, ToDto(trade), tradeEvent.Message);
            }
        }
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

    /// <summary>Adds realized P&amp;L to the linked account's CurrentBalance, if any.</summary>
    private async Task ApplyPnlToAccountAsync(Trade trade, CancellationToken cancellationToken)
    {
        if (trade.TradingAccountId is null || trade.Pnl is null)
            return;

        var account = await dbContext.TradingAccounts
            .FirstOrDefaultAsync(a => a.Id == trade.TradingAccountId.Value, cancellationToken);

        if (account is null)
            return;

        account.CurrentBalance += trade.Pnl.Value;

        logger.LogInformation(
            "Account {AccountId} CurrentBalance updated by {Pnl} → {CurrentBalance}",
            account.Id, trade.Pnl.Value, account.CurrentBalance);
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
            Id:               t.Id,
            SymbolCode:       t.Symbol.Code,
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
            UnrealizedPnl:    unrealizedPnl,
            TradingAccountId: t.TradingAccountId);
    }

    private static decimal? CalculatePnl(Trade trade, decimal closedPrice)
    {
        if (trade.EntryPrice is null)
            return null;

        return trade.SideId == (int)TradeSide.Buy
            ? (closedPrice - trade.EntryPrice.Value) * trade.Quantity
            : (trade.EntryPrice.Value - closedPrice) * trade.Quantity;
    }

    private void PublishTradeEvent(string type, TradeResponseDto trade, string message)
    {
        tradeEventPublisher.Publish(new TradeEventDto(
            Type: type,
            TradingAccountId: trade.TradingAccountId,
            TradeId: trade.Id,
            SymbolCode: trade.SymbolCode,
            Message: message,
            CreatedAt: timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            Trade: trade));
    }
}
