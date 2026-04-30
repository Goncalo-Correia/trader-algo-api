using System.ComponentModel.DataAnnotations;

namespace TraderAlgoApi.Dtos.TradeBots;

public sealed record CreateTradeBotRequestDto(
    long TradingAccountId,
    [Required, MaxLength(20)] string SymbolCode,
    [Required, MaxLength(10)] string IntervalCode,
    [Range(0.00000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    decimal Quantity,
    decimal? StopLoss,
    decimal? TakeProfit,
    bool IsEnabled);
