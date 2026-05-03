using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Lookups;

namespace TraderAlgoApi.Models;

[Table("trading_accounts")]
public sealed class TradingAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Precision(28, 10)]
    public decimal InitialBalance { get; set; }

    [Precision(28, 10)]
    public decimal CurrentBalance { get; set; }

    public int TradingStrategyId { get; set; }

    [ForeignKey(nameof(TradingStrategyId))]
    public TradingStrategy TradingStrategy { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Trade> Trades { get; set; } = [];

    public TradeBot? TradeBot { get; set; }
}
