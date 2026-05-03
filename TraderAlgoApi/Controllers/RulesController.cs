using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Rules.Macd;
using TraderAlgoApi.Dtos.Rules.Rsi;
using TraderAlgoApi.Dtos.Rules.Sma;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/rules")]
public sealed class RulesController(
    ITradingRuleContextService contextService,
    SmaTradingRule smaRule,
    RsiTradingRule rsiRule,
    MacdTradingRule macdRule) : ControllerBase
{
    [HttpGet("sma/evaluate")]
    public async Task<ActionResult<SmaRuleEvaluationResponseDto>> EvaluateSma(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        CancellationToken cancellationToken)
    {
        var (context, error) = await LoadContextAsync(symbol, interval, cancellationToken);
        if (error is not null) return error;

        return Ok(new SmaRuleEvaluationResponseDto(
            SymbolCode: context!.SymbolCode,
            IntervalCode: context.IntervalCode,
            Open: context.CurrentOpen,
            High: context.CurrentHigh,
            Low: context.CurrentLow,
            Close: context.CurrentClose,
            Sma20: context.CurrentSma20,
            Sma100: context.CurrentSma100,
            IsSma20AboveSma100: smaRule.IsSma20AboveSma100(context),
            IsSma20BelowSma100: smaRule.IsSma20BelowSma100(context),
            IsPriceRetestingSma20: smaRule.IsPriceRetestingSma20(context),
            LastThreeCandlesAboveSma20: smaRule.LastThreeCandlesAboveSma20(context),
            LastThreeCandlesBelowSma20: smaRule.LastThreeCandlesBelowSma20(context),
            ShouldEnterLong: smaRule.ShouldEnterLong(context),
            ShouldEnterShort: smaRule.ShouldEnterShort(context)));
    }

    [HttpGet("rsi/evaluate")]
    public async Task<ActionResult<RsiRuleEvaluationResponseDto>> EvaluateRsi(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        CancellationToken cancellationToken)
    {
        var (context, error) = await LoadContextAsync(symbol, interval, cancellationToken);
        if (error is not null) return error;

        return Ok(new RsiRuleEvaluationResponseDto(
            SymbolCode: context!.SymbolCode,
            IntervalCode: context.IntervalCode,
            Close: context.CurrentClose,
            Rsi: context.CurrentRsi,
            RsiSmooth: context.CurrentRsiSmooth,
            IsRsiBelow30: rsiRule.IsRsiBelow30(context),
            IsRsiAbove70: rsiRule.IsRsiAbove70(context),
            IsRsiAboveSmoothRsi: rsiRule.IsRsiAboveSmoothRsi(context),
            IsRsiBelowSmoothRsi: rsiRule.IsRsiBelowSmoothRsi(context),
            ShouldEnterLong: rsiRule.ShouldEnterLong(context),
            ShouldEnterShort: rsiRule.ShouldEnterShort(context)));
    }

    [HttpGet("macd/evaluate")]
    public async Task<ActionResult<MacdRuleEvaluationResponseDto>> EvaluateMacd(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        CancellationToken cancellationToken)
    {
        var (context, error) = await LoadContextAsync(symbol, interval, cancellationToken);
        if (error is not null) return error;

        return Ok(new MacdRuleEvaluationResponseDto(
            SymbolCode: context!.SymbolCode,
            IntervalCode: context.IntervalCode,
            Close: context.CurrentClose,
            MacdLine: context.CurrentMacdLine,
            SignalLine: context.CurrentSignalLine,
            Histogram: context.CurrentHistogram,
            PreviousHistogram: context.PreviousHistogram,
            IsMacdLineAboveSignalLine: macdRule.IsMacdLineAboveSignalLine(context),
            IsMacdLineBelowSignalLine: macdRule.IsMacdLineBelowSignalLine(context),
            IsHistogramAboveZero: macdRule.IsHistogramAboveZero(context),
            IsHistogramBelowZero: macdRule.IsHistogramBelowZero(context),
            IsHistogramIncreasing: macdRule.IsHistogramIncreasing(context),
            IsHistogramDecreasing: macdRule.IsHistogramDecreasing(context),
            ShouldEnterLong: macdRule.ShouldEnterLong(context),
            ShouldEnterShort: macdRule.ShouldEnterShort(context)));
    }

    private async Task<(TradingRuleContext? Context, ActionResult? Error)> LoadContextAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return (null, BadRequest("symbol is required."));

        if (string.IsNullOrWhiteSpace(interval))
            return (null, BadRequest("interval is required."));

        var context = await contextService.GetLatestContextAsync(symbol, interval, cancellationToken);

        if (context is null)
            return (null, NotFound($"Insufficient candle data for {symbol}/{interval}."));

        return (context, null);
    }
}
