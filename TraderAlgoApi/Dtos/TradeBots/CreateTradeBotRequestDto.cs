using System.ComponentModel.DataAnnotations;

namespace TraderAlgoApi.Dtos.TradeBots;

public sealed record CreateTradeBotRequestDto(
    long TradingAccountId,
    [Range(1, int.MaxValue, ErrorMessage = "TradingStrategyId must be a valid strategy ID.")]
    int TradingStrategyId,
    [Required, MaxLength(20)] string SymbolCode,
    [Required, MaxLength(10)] string IntervalCode,
    [Range(0.00000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    decimal Quantity,
    decimal? StopLoss,
    decimal? TakeProfit,
    bool IsEnabled,
    decimal? Breakeven = null,
    decimal? BreakevenStop = null,
    bool IsNySessionOnly = false,
    bool Delay = false,
    decimal? DailyProfitGoal = null,
    int? MaxLossesPerDay = null,
    int? MaxCandlesPerTrade = null,
    decimal Fee = 0);
