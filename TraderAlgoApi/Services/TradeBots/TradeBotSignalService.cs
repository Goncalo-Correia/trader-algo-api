using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Ml;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Backtests;
using TraderAlgoApi.Services.Ml;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;
using TraderAlgoApi.Services.Rules.SmaMacd;

namespace TraderAlgoApi.Services.TradeBots;

public sealed class TradeBotSignalService(
    ApplicationDbContext dbContext,
    ITradingRuleContextService contextService,
    IMlConnectorService mlConnector,
    TimeProvider timeProvider,
    SmaTradingRule smaRule,
    RsiTradingRule rsiRule,
    MacdTradingRule macdRule,
    SmaMacdTradingRule smaMacdRule,
    ILogger<TradeBotSignalService> logger) : ITradeBotSignalService
{
    // ML action codes returned by the Python sidecar
    private const int MlActionEnterLong  = 1;
    private const int MlActionEnterShort = 2;

    public async Task<TradeBotSignalResult> EvaluateAsync(
        TradeBot tradeBot,
        CancellationToken cancellationToken = default)
    {
        var context = await contextService.GetLatestContextAsync(
            tradeBot.Symbol.Code,
            tradeBot.Interval.Code,
            cancellationToken);

        if (context is null)
            return new TradeBotSignalResult(TradeBotSignal.None, "Insufficient candle data.");

        if ((TradingStrategy)tradeBot.TradingStrategyId == TradingStrategy.MlPolicy)
            return await EvaluateMlAsync(tradeBot, context, cancellationToken);

        ITradingRule? rule = (TradingStrategy)tradeBot.TradingStrategyId switch
        {
            TradingStrategy.Sma     => smaRule,
            TradingStrategy.Rsi     => rsiRule,
            TradingStrategy.Macd    => macdRule,
            TradingStrategy.SmaMacd => smaMacdRule,
            _ => null
        };

        if (rule is null)
            return new TradeBotSignalResult(TradeBotSignal.None, "Unsupported trading strategy.");

        if (rule.ShouldEnterLong(context))
            return new TradeBotSignalResult(TradeBotSignal.EnterLong, "Strategy signaled long entry.");

        if (rule.ShouldEnterShort(context))
            return new TradeBotSignalResult(TradeBotSignal.EnterShort, "Strategy signaled short entry.");

        return new TradeBotSignalResult(TradeBotSignal.None, "No entry signal.");
    }

    // -------------------------------------------------------------------------

    private async Task<TradeBotSignalResult> EvaluateMlAsync(
        TradeBot tradeBot,
        TradingRuleContext context,
        CancellationToken cancellationToken)
    {
        var policy = await LoadPolicyAsync(tradeBot, cancellationToken);
        if (policy is null)
            return new TradeBotSignalResult(TradeBotSignal.None, "ML policy strategy requires a linked policy.");

        if (tradeBot.TradingAccountId is not long accountId || tradeBot.TradingAccount is null)
            return new TradeBotSignalResult(TradeBotSignal.None, "ML policy strategy requires a trading account.");

        var openTrade = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.BacktestId == null &&
                        t.TradingAccountId == accountId &&
                        (t.StatusId == (int)TradeStatus.Active || t.StatusId == (int)TradeStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (openTrade is not null)
            return new TradeBotSignalResult(TradeBotSignal.None, "ML policy is entry-only while a trade is open or pending.");

        var latestCandle = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == tradeBot.SymbolId && k.IntervalId == tradeBot.IntervalId)
            .OrderByDescending(k => k.OpenTime)
            .Select(k => new { k.Volume, k.TakerBuyBaseAssetVolume, AtrValue = (decimal?)(k.Atr != null ? k.Atr.AtrValue : null) })
            .FirstOrDefaultAsync(cancellationToken);

        var closedTrades = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.BacktestId == null &&
                        t.TradingAccountId == accountId &&
                        t.StatusId == (int)TradeStatus.Closed)
            .OrderByDescending(t => t.ClosedAt ?? t.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var today = BacktestSimulationEngine.EasternDay(timeProvider.GetUtcNow());
        // Daily realized PnL uses trades that CLOSED today (Eastern) — the same close-day convention
        // as the entry-limit gate (TradeBotMonitorService) and the backtest's dailyStats, so the
        // sidecar sees identical daily accounting whether live or backtesting.
        var currentDailyPnl = closedTrades
            .Where(t => t.ClosedAt.HasValue && BacktestSimulationEngine.EasternDay(t.ClosedAt.Value) == today)
            .Sum(t => t.Pnl ?? 0m);
        // Trades *taken* today counts entries OPENED today (Eastern) — matches the backtest observation.
        var tradesTakenToday = closedTrades
            .Count(t => t.OpenedAt.HasValue && BacktestSimulationEngine.EasternDay(t.OpenedAt.Value) == today);
        var currentDailyDrawdownCash = Math.Max(0m, -currentDailyPnl);
        // The ML training environment expects current_daily_drawdown as a FRACTION in [0, 1] of the
        // day-start balance, not absolute cash. day-start = current balance minus today's PnL.
        var dayStartBalance = tradeBot.TradingAccount.CurrentBalance - currentDailyPnl;
        var currentDailyDrawdown = dayStartBalance > 0m
            ? currentDailyDrawdownCash / dayStartBalance
            : 0m;
        var (winsInRow, lossesInRow) = CountStreaks(closedTrades);
        var lastTrade = closedTrades.FirstOrDefault();
        var candlesSinceLastTradeClosed = await CountCandlesSinceLastTradeClosedAsync(
            tradeBot,
            lastTrade,
            cancellationToken);

        var request = new MlDecideRequest(
            MlPolicyId:    policy.Id,
            Symbol:       tradeBot.Symbol.Code,
            Interval:     tradeBot.Interval.Code,
            ModelId:      policy.Id.ToString(),
            Candle: new MlCandleFeatures(
                Open:           context.CurrentOpen,
                High:           context.CurrentHigh,
                Low:            context.CurrentLow,
                Close:          context.CurrentClose,
                Volume:         latestCandle?.Volume ?? 0m,
                TakerBuyVolume: latestCandle?.TakerBuyBaseAssetVolume ?? 0m,
                Sma20:          context.CurrentSma20,
                Sma100:         context.CurrentSma100,
                Rsi:            context.CurrentRsi,
                RsiSmooth:      context.CurrentRsiSmooth,
                MacdLine:       context.CurrentMacdLine,
                SignalLine:     context.CurrentSignalLine,
                Histogram:      context.CurrentHistogram),
            Position:      0,
            InitialAccountBalance: tradeBot.TradingAccount.InitialBalance,
            CurrentAccountBalance: tradeBot.TradingAccount.CurrentBalance,
            CurrentDailyPnl: currentDailyPnl,
            CurrentDailyDrawdown: currentDailyDrawdown,
            WinsInRow: winsInRow,
            LossesInRow: lossesInRow,
            TradesTakenToday: tradesTakenToday,
            DailyProfitTargetReached: policy.DailyProfit > 0m && currentDailyPnl >= policy.DailyProfit,
            DailyDrawdownReached: policy.DailyDrawdownLimit > 0m && currentDailyDrawdownCash >= policy.DailyDrawdownLimit,
            LastTradePnl: lastTrade?.Pnl ?? 0m,
            LastTradeCloseReason: CloseReasonName(lastTrade),
            CandlesSinceLastTradeClosed: candlesSinceLastTradeClosed,
            ConfiguredMaxCandlesPerTrade: policy.MaxCandlesPerTrade,
            FeeRate: policy.Fee,
            UnrealizedPnl: 0m);

        MlDecideResponse response;
        try
        {
            response = await mlConnector.DecideAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A policy whose live model predates the latest ML release (or any other ML error) is
            // rejected at inference time. Treat as 'hold' so one policy's failure can't abort the
            // whole tradebot batch, and so bots stay safe until the policy is retrained.
            logger.LogWarning(
                ex,
                "ML decide failed for policy {PolicyId}; treating as hold.",
                policy.Id);
            return new TradeBotSignalResult(TradeBotSignal.None, "ML decide unavailable (treated as hold).");
        }

        if (response.Action is not (MlActionEnterLong or MlActionEnterShort))
            return new TradeBotSignalResult(TradeBotSignal.None, $"ML policy: {response.ActionName}.");

        // The sidecar returns both bracket multipliers on an entry; without them we cannot size the
        // stop/take-profit, so hold rather than open an unbracketed trade.
        if (response.SlAtrMult is not decimal slAtrMult || response.TpRMult is not decimal tpRMult)
        {
            logger.LogWarning(
                "ML policy {PolicyId} signaled an entry ({Action}) without bracket multipliers; treating as hold.",
                policy.Id, response.ActionName);
            return new TradeBotSignalResult(TradeBotSignal.None, "ML entry missing bracket (treated as hold).");
        }

        var atrAtEntry = BacktestSimulationEngine.AtrAtEntryOrFallback(latestCandle?.AtrValue);
        var bracket = new MlBracket(slAtrMult, tpRMult, atrAtEntry);
        var signal = response.Action == MlActionEnterLong ? TradeBotSignal.EnterLong : TradeBotSignal.EnterShort;
        var direction = response.Action == MlActionEnterLong ? "long" : "short";

        return new TradeBotSignalResult(
            signal,
            $"ML policy signaled {direction} (confidence={response.Confidence:P1}).",
            bracket);
    }

    private async Task<MlPolicy?> LoadPolicyAsync(TradeBot tradeBot, CancellationToken cancellationToken)
    {
        if (tradeBot.MlPolicy is not null)
            return tradeBot.MlPolicy;

        if (tradeBot.MlPolicyId is not long policyId)
            return null;

        return await dbContext.MlPolicies
            .AsNoTracking()
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken);
    }

    private async Task<int> CountCandlesSinceLastTradeClosedAsync(
        TradeBot tradeBot,
        Trade? lastTrade,
        CancellationToken cancellationToken)
    {
        if (lastTrade?.ClosedAt is not DateTimeOffset closedAt)
            return 0;

        return await dbContext.KlineData
            .AsNoTracking()
            .CountAsync(
                k => k.SymbolId == tradeBot.SymbolId &&
                     k.IntervalId == tradeBot.IntervalId &&
                     k.OpenTime > closedAt,
                cancellationToken);
    }

    private static (int Wins, int Losses) CountStreaks(IReadOnlyList<Trade> closedTrades)
    {
        if (closedTrades.Count == 0 || closedTrades[0].Pnl is null or 0m)
            return (0, 0);

        var count = 0;
        var winning = closedTrades[0].Pnl > 0m;
        foreach (var trade in closedTrades)
        {
            if (trade.Pnl is null)
                break;

            if (winning && trade.Pnl > 0m)
                count++;
            else if (!winning && trade.Pnl < 0m)
                count++;
            else
                break;
        }

        return winning ? (count, 0) : (0, count);
    }

    private static string CloseReasonName(Trade? trade) =>
        trade?.CloseReasonId switch
        {
            var id when id == (int)TradeCloseReason.StopLoss => "stop_loss",
            var id when id == (int)TradeCloseReason.TakeProfit => "take_profit",
            var id when id == (int)TradeCloseReason.BotSignal => "bot_signal",
            var id when id == (int)TradeCloseReason.Manual => "manual",
            _ => string.Empty
        };
}
