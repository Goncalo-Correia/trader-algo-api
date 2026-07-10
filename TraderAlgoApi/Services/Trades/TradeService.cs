using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    // Upper bounds on unbounded history reads so a large table can't produce a runaway payload.
    private const int MaxHistoryTrades  = 2_000;
    private const int MaxBacktestTrades = 20_000;

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
            .Where(t => t.BacktestId == null &&
                        (t.StatusId == (int)TradeStatus.Pending || t.StatusId == (int)TradeStatus.Active));

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
                AtrAtEntry       = request.AtrAtEntry,
                StatusId         = (int)TradeStatus.Active,
                CreatedAt        = now,
                OpenedAt         = now,
                TradingAccountId = request.TradingAccountId,
                Fee              = request.Fee
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
                AtrAtEntry       = request.AtrAtEntry,
                StatusId         = (int)TradeStatus.Pending,
                CreatedAt        = now,
                TradingAccountId = request.TradingAccountId,
                Fee              = request.Fee
            };
        }

        dbContext.Trades.Add(trade);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The pre-check above is racy; the filtered unique index on open live trades is the real
            // guard. A concurrent create that slipped past the check trips it here — surface the same
            // "already open" error rather than a raw DB fault.
            throw new InvalidOperationException(
                request.TradingAccountId is long blockedAccountId
                    ? $"A pending or active trade already exists for trading account {blockedAccountId}. Close it before opening a new one."
                    : $"A pending or active trade already exists for {request.SymbolCode}. Close it before opening a new one.");
        }

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
        // AsNoTracking: the close is persisted with a guarded atomic UPDATE below, not via the change
        // tracker, so we only need this read for validation, pricing, and the response DTO.
        var trade = await TradeWithNavigations()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Trade {id} not found.");

        if (trade.StatusId != (int)TradeStatus.Active)
            throw new InvalidOperationException(
                $"Trade {id} cannot be stopped: current status is {(TradeStatus)trade.StatusId}.");

        var price = await ResolveCurrentPriceAsync(trade.Symbol.Code, cancellationToken);
        var now   = timeProvider.GetUtcNow();
        var pnl   = CalculatePnl(trade, price);

        // Flip Active→Closed and apply the account PnL atomically. The UPDATE is guarded on the row
        // still being Active, so if a tick-trigger close (EvaluatePriceAsync) races this one only a
        // single writer wins and the realized PnL is applied exactly once. The retrying execution
        // strategy forbids a bare user transaction, so the unit runs inside
        // CreateExecutionStrategy().ExecuteAsync to be retried atomically.
        await dbContext.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(async ct =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

                var closed = await dbContext.Trades
                    .Where(t => t.Id == id && t.StatusId == (int)TradeStatus.Active)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.StatusId, (int)TradeStatus.Closed)
                        .SetProperty(t => t.ClosedAt, now)
                        .SetProperty(t => t.ClosedPrice, price)
                        .SetProperty(t => t.CloseReasonId, (int)closeReason)
                        .SetProperty(t => t.Pnl, pnl),
                        ct);

                if (closed == 0)
                    throw new InvalidOperationException(
                        $"Trade {id} cannot be stopped: it is no longer active.");

                await ApplyPnlToAccountAsync(trade.TradingAccountId, pnl, ct);
                await transaction.CommitAsync(ct);
            }, cancellationToken);

        // Mirror the persisted transition onto the in-memory copy for the response DTO / event.
        trade.StatusId      = (int)TradeStatus.Closed;
        trade.ClosedAt      = now;
        trade.ClosedPrice   = price;
        trade.CloseReasonId = (int)closeReason;
        trade.Pnl           = pnl;

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
            .Where(t => t.BacktestId == null &&
                        t.TradingAccountId == tradingAccountId &&
                        (t.StatusId == (int)TradeStatus.Active || t.StatusId == (int)TradeStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return trades.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TradeResponseDto>> GetHistoryAsync(
        long tradingAccountId,
        CancellationToken cancellationToken = default)
    {
        // Cap the result so an account with a very long history can't produce an unbounded payload;
        // most-recent-first, so the cap keeps the rows a UI actually shows.
        var trades = await TradeWithNavigations()
            .AsNoTracking()
            .Where(t => t.BacktestId == null &&
                        t.TradingAccountId == tradingAccountId &&
                        (t.StatusId == (int)TradeStatus.Closed || t.StatusId == (int)TradeStatus.Cancelled))
            .OrderByDescending(t => t.ClosedAt)
            .Take(MaxHistoryTrades)
            .ToListAsync(cancellationToken);

        return trades.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TradeResponseDto>> GetByBacktestAsync(
        long backtestId,
        CancellationToken cancellationToken = default)
    {
        // Defensive upper bound: a backtest's trade count is bounded by its run, but a pathological
        // run shouldn't be able to stream an unbounded list back through this reconciliation read.
        var trades = await TradeWithNavigations()
            .AsNoTracking()
            .Where(t => t.BacktestId == backtestId)
            .OrderBy(t => t.CreatedAt)
            .Take(MaxBacktestTrades)
            .ToListAsync(cancellationToken);

        return trades.Select(ToDto).ToList();
    }

    public async Task EvaluatePriceAsync(
        string symbol,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        // Resolve the symbol to its indexed id first so the open-trades query can seek the
        // (SymbolId, StatusId) index directly instead of joining through the Symbol navigation.
        var symbolId = await dbContext.Symbols
            .Where(s => s.Code == symbol)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (symbolId is null)
            return;

        // AsNoTracking + guarded UPDATEs: each transition is persisted with an atomic UPDATE below,
        // so we never write these entities back through the change tracker.
        var trades = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.BacktestId == null &&
                        t.SymbolId == symbolId.Value &&
                        (t.StatusId == (int)TradeStatus.Pending || t.StatusId == (int)TradeStatus.Active))
            .ToListAsync(cancellationToken);

        if (trades.Count == 0)
            return;

        var now     = timeProvider.GetUtcNow();
        var tradeEvents = new List<(long TradeId, string Type, string Message)>();

        foreach (var trade in trades)
        {
            if (trade.StatusId == (int)TradeStatus.Pending)
            {
                if (!TryFillLimit(trade, price, now))
                    continue;

                // Guarded Pending→Active fill: no-op if another writer already filled or cancelled it.
                var filled = await dbContext.Trades
                    .Where(t => t.Id == trade.Id && t.StatusId == (int)TradeStatus.Pending)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.StatusId, (int)TradeStatus.Active)
                        .SetProperty(t => t.EntryPrice, trade.EntryPrice)
                        .SetProperty(t => t.OpenedAt, trade.OpenedAt),
                        cancellationToken);

                if (filled > 0)
                    tradeEvents.Add((trade.Id, "TradeOpened", "Pending trade filled."));
            }
            else
            {
                if (!TryTriggerSLTP(trade, price, now))
                    continue;

                // Guarded Active→Closed transition + atomic account PnL. If a manual close or another
                // tick already closed this trade, the UPDATE matches no row and we apply no PnL. The
                // retrying execution strategy forbids a bare user transaction, so the unit runs inside
                // CreateExecutionStrategy().ExecuteAsync to be retried atomically.
                var didClose = await dbContext.Database
                    .CreateExecutionStrategy()
                    .ExecuteAsync(async ct =>
                    {
                        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

                        var closed = await dbContext.Trades
                            .Where(t => t.Id == trade.Id && t.StatusId == (int)TradeStatus.Active)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(t => t.StatusId, (int)TradeStatus.Closed)
                                .SetProperty(t => t.ClosedAt, trade.ClosedAt)
                                .SetProperty(t => t.ClosedPrice, trade.ClosedPrice)
                                .SetProperty(t => t.CloseReasonId, trade.CloseReasonId)
                                .SetProperty(t => t.Pnl, trade.Pnl),
                                ct);

                        if (closed > 0)
                        {
                            await ApplyPnlToAccountAsync(trade.TradingAccountId, trade.Pnl, ct);
                            await transaction.CommitAsync(ct);
                            return true;
                        }

                        await transaction.RollbackAsync(ct);
                        return false;
                    }, cancellationToken);

                if (didClose)
                    tradeEvents.Add((trade.Id, "TradeClosed", "Trade closed by price trigger."));
            }
        }

        if (tradeEvents.Count == 0)
            return;

        var changedIds = tradeEvents.Select(e => e.TradeId).Distinct().ToList();
        var changedTrades = await TradeWithNavigations()
            .AsNoTracking()
            .Where(t => changedIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        foreach (var tradeEvent in tradeEvents)
        {
            if (changedTrades.TryGetValue(tradeEvent.TradeId, out var trade))
                PublishTradeEvent(tradeEvent.Type, ToDto(trade), tradeEvent.Message);
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

    /// <summary>
    /// Adds realized P&amp;L to the linked account's CurrentBalance via an atomic in-database
    /// increment, so concurrent closes can't lose an update the way a tracked read-modify-write
    /// would. Callers run this inside the same transaction as the guarded trade close.
    /// </summary>
    private async Task ApplyPnlToAccountAsync(long? tradingAccountId, decimal? pnl, CancellationToken cancellationToken)
    {
        if (tradingAccountId is not long accountId || pnl is not decimal delta)
            return;

        var rows = await dbContext.TradingAccounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.CurrentBalance, a => a.CurrentBalance + delta),
                cancellationToken);

        if (rows > 0)
            logger.LogInformation("Account {AccountId} CurrentBalance incremented by {Pnl}", accountId, delta);
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
            Fee:              t.Fee,
            Pnl:              t.Pnl,
            AccountPnl:       null,
            UnrealizedPnl:    unrealizedPnl,
            TradingAccountId: t.TradingAccountId,
            BacktestId:       t.BacktestId);
    }

    private static decimal? CalculatePnl(Trade trade, decimal closedPrice)
    {
        if (trade.EntryPrice is null)
            return null;

        var rawPnl = trade.SideId == (int)TradeSide.Buy
            ? (closedPrice - trade.EntryPrice.Value) * trade.Quantity
            : (trade.EntryPrice.Value - closedPrice) * trade.Quantity;

        return rawPnl - trade.Fee;
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
