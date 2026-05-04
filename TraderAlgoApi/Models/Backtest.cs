using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Models;

[Table("backtests")]
public sealed class Backtest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int SymbolId { get; set; }

    [ForeignKey(nameof(SymbolId))]
    public Symbol Symbol { get; set; } = null!;

    [Required]
    public int IntervalId { get; set; }

    [ForeignKey(nameof(IntervalId))]
    public Interval Interval { get; set; } = null!;

    [Required]
    public int TradingStrategyId { get; set; }

    [ForeignKey(nameof(TradingStrategyId))]
    public Lookups.TradingStrategy TradingStrategy { get; set; } = null!;

    public long? TradeBotId { get; set; }

    [ForeignKey(nameof(TradeBotId))]
    public TradeBot? TradeBot { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public BacktestStatus Status { get; set; }

    [Precision(28, 10)]
    public decimal InitialBalance { get; set; }

    [Precision(28, 10)]
    public decimal? FinalBalance { get; set; }

    [Precision(28, 10)]
    public decimal? Pnl { get; set; }

    [Precision(28, 10)]
    public decimal Quantity { get; set; }

    [Precision(28, 10)]
    public decimal? StopLoss { get; set; }

    [Precision(28, 10)]
    public decimal? TakeProfit { get; set; }

    [Precision(28, 10)]
    public decimal? Breakeven { get; set; }

    public bool IsNySessionOnly { get; set; }

    [Precision(28, 10)]
    public decimal? DailyProfitGoal { get; set; }

    public int? MaxLossesPerDay { get; set; }

    public int CandleCount { get; set; }

    public ICollection<Trade> Trades { get; set; } = [];

    public ICollection<TradeBot> TradeBots { get; set; } = [];
}
