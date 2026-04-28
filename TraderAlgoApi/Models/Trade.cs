using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Models;

[Table("trades")]
public sealed class Trade
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required, MaxLength(20)]
    public string SymbolCode { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? IntervalCode { get; set; }

    public TradeSide Side { get; set; }

    public TradeOrderType OrderType { get; set; }

    [Precision(28, 10)]
    public decimal Quantity { get; set; }

    /// <summary>Limit price for limit orders; null for market orders.</summary>
    [Precision(28, 10)]
    public decimal? RequestedPrice { get; set; }

    /// <summary>Actual fill price; set when status transitions to Active.</summary>
    [Precision(28, 10)]
    public decimal? EntryPrice { get; set; }

    [Precision(28, 10)]
    public decimal? StopLoss { get; set; }

    [Precision(28, 10)]
    public decimal? TakeProfit { get; set; }

    public TradeStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    [Precision(28, 10)]
    public decimal? ClosedPrice { get; set; }

    public TradeCloseReason? CloseReason { get; set; }
}
