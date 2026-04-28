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

    public int SideId { get; set; }

    public int OrderTypeId { get; set; }

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

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    [Precision(28, 10)]
    public decimal? ClosedPrice { get; set; }

    public int? CloseReasonId { get; set; }

    // -------------------------------------------------------------------------
    // Computed properties — not persisted, provide typed enum access throughout
    // the business logic layer without leaking raw int IDs.
    // -------------------------------------------------------------------------

    [NotMapped]
    public TradeSide Side
    {
        get => (TradeSide)SideId;
        set => SideId = (int)value;
    }

    [NotMapped]
    public TradeOrderType OrderType
    {
        get => (TradeOrderType)OrderTypeId;
        set => OrderTypeId = (int)value;
    }

    [NotMapped]
    public TradeStatus Status
    {
        get => (TradeStatus)StatusId;
        set => StatusId = (int)value;
    }

    [NotMapped]
    public TradeCloseReason? CloseReason
    {
        get => CloseReasonId is int id ? (TradeCloseReason)id : null;
        set => CloseReasonId = value is TradeCloseReason reason ? (int)reason : null;
    }
}
