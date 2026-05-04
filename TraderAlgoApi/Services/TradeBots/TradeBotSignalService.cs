using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;

namespace TraderAlgoApi.Services.TradeBots;

public sealed class TradeBotSignalService(
    ITradingRuleContextService contextService,
    SmaTradingRule smaRule,
    RsiTradingRule rsiRule,
    MacdTradingRule macdRule) : ITradeBotSignalService
{
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

        ITradingRule? rule = (TradingStrategy)tradeBot.TradingStrategyId switch
        {
            TradingStrategy.Sma => smaRule,
            TradingStrategy.Rsi => rsiRule,
            TradingStrategy.Macd => macdRule,
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
}
