using TraderAlgoApi.Dtos.Ml;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Ml;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;
using TraderAlgoApi.Services.Rules.SmaMacd;

namespace TraderAlgoApi.Services.TradeBots;

public sealed class TradeBotSignalService(
    ITradingRuleContextService contextService,
    IMlConnectorService mlConnector,
    MlConnectorOptions mlOptions,
    SmaTradingRule smaRule,
    RsiTradingRule rsiRule,
    MacdTradingRule macdRule,
    SmaMacdTradingRule smaMacdRule) : ITradeBotSignalService
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
        var request = new MlDecideRequest(
            Symbol:       tradeBot.Symbol.Code,
            Interval:     tradeBot.Interval.Code,
            ModelId:      mlOptions.ModelId,
            Candle: new MlCandleFeatures(
                Open:           context.CurrentOpen,
                High:           context.CurrentHigh,
                Low:            context.CurrentLow,
                Close:          context.CurrentClose,
                Volume:         0m,
                TakerBuyVolume: 0m,
                Sma20:          context.CurrentSma20,
                Sma100:         context.CurrentSma100,
                Rsi:            context.CurrentRsi,
                RsiSmooth:      context.CurrentRsiSmooth,
                MacdLine:       context.CurrentMacdLine,
                SignalLine:     context.CurrentSignalLine,
                Histogram:      context.CurrentHistogram),
            Position:      0,
            CandlesHeld:   0,
            UnrealizedPnl: 0m);

        var response = await mlConnector.DecideAsync(request, cancellationToken);

        return response.Action switch
        {
            MlActionEnterLong  => new TradeBotSignalResult(TradeBotSignal.EnterLong,  $"ML policy signaled long (confidence={response.Confidence:P1})."),
            MlActionEnterShort => new TradeBotSignalResult(TradeBotSignal.EnterShort, $"ML policy signaled short (confidence={response.Confidence:P1})."),
            _ => new TradeBotSignalResult(TradeBotSignal.None, $"ML policy: {response.ActionName}.")
        };
    }
}
