using System.ComponentModel.DataAnnotations;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Dtos.TradingAccounts;

public sealed record CreateTradingAccountRequestDto(
    [Required, MaxLength(100)] string Name,
    [Range(0.01, double.MaxValue, ErrorMessage = "InitialBalance must be greater than zero.")]
    decimal         InitialBalance,
    TradingStrategy TradingStrategy);
