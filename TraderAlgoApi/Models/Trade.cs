using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Lookups;

namespace TraderAlgoApi.Models;

[Table("trades")]
public sealed class Trade
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int SymbolId { get; set; }

    [ForeignKey(nameof(SymbolId))]
    public Symbol Symbol { get; set; } = null!;

    public int? IntervalId { get; set; }

    [ForeignKey(nameof(IntervalId))]
    public Interval? Interval { get; set; }

    public int SideId { get; set; }

    [ForeignKey(nameof(SideId))]
    public TradeSide Side { get; set; } = null!;

    public int OrderTypeId { get; set; }

    [ForeignKey(nameof(OrderTypeId))]
    public TradeOrderType OrderType { get; set; } = null!;

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

    public int StatusId { get; set; }

    [ForeignKey(nameof(StatusId))]
    public TradeStatus Status { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    [Precision(28, 10)]
    public decimal? ClosedPrice { get; set; }

    /// <summary>Realized profit/loss, computed when the trade closes. Null while pending or active.</summary>
    [Precision(28, 10)]
    public decimal? Pnl { get; set; }

    public int? CloseReasonId { get; set; }

    [ForeignKey(nameof(CloseReasonId))]
    public TradeCloseReason? CloseReason { get; set; }

    public long? TradingAccountId { get; set; }

    [ForeignKey(nameof(TradingAccountId))]
    public TradingAccount? TradingAccount { get; set; }

    public long? BacktestId { get; set; }

    [ForeignKey(nameof(BacktestId))]
    public Backtest? Backtest { get; set; }
}
