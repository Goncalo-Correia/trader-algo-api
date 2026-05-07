using System.ComponentModel.DataAnnotations;

namespace TraderAlgoApi.Dtos.TradingAccounts;

public sealed record CreateTradingAccountRequestDto(
    [Required, MaxLength(100)] string Name,
    [Range(0.01, double.MaxValue, ErrorMessage = "InitialBalance must be greater than zero.")]
    decimal InitialBalance);
