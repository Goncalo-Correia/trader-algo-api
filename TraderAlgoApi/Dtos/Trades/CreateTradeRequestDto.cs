using System.ComponentModel.DataAnnotations;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Dtos.Trades;

public sealed record CreateTradeRequestDto(
    [Required, MaxLength(20)] string SymbolCode,
    [MaxLength(10)]           string? IntervalCode,
    TradeSide                 Side,
    TradeOrderType            OrderType,
    [Range(0.00000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    decimal                   Quantity,
    [Range(0.00000001, double.MaxValue, ErrorMessage = "LimitPrice must be greater than zero.")]
    decimal?                  LimitPrice,
    decimal?                  StopLoss,
    decimal?                  TakeProfit,
    long?                     TradingAccountId,
    decimal                   Fee = 0,
    // ATR-at-entry for ML-policy bracket trades; persisted onto the Trade so the live breakeven
    // ratchet can scale its offsets. Null for indicator strategies.
    decimal?                  AtrAtEntry = null);
