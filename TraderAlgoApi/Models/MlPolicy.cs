using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

/// <summary>
/// A reusable training configuration: which model to train on which symbol/interval, plus all
/// PPO/risk hyperparameters. One policy can have many <see cref="MlTrainingRun"/>s (each run trains
/// this policy over a chosen date range). Risk params are absolute amounts (matching backtests).
/// </summary>
[Table("ml_policies")]
public sealed class MlPolicy
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public int SymbolId { get; set; }

    [ForeignKey(nameof(SymbolId))]
    public Symbol Symbol { get; set; } = null!;

    public int IntervalId { get; set; }

    [ForeignKey(nameof(IntervalId))]
    public Interval Interval { get; set; } = null!;

    public int TotalTimesteps { get; set; }

    [Precision(28, 10)]
    public decimal InitialBalance { get; set; }

    [Precision(28, 10)]
    public decimal Quantity { get; set; }

    [Precision(28, 10)]
    public decimal TakeProfit { get; set; }

    [Precision(28, 10)]
    public decimal StopLoss { get; set; }

    [Precision(28, 10)]
    public decimal Breakeven { get; set; }

    [Precision(28, 10)]
    public decimal BreakevenStop { get; set; }

    [Precision(28, 10)]
    public decimal Fee { get; set; }

    [Precision(28, 10)]
    public decimal Slippage { get; set; }

    [Precision(28, 10)]
    public decimal DailyProfit { get; set; }

    [Precision(28, 10)]
    public decimal DailyDrawdownLimit { get; set; }

    public int MaxCandlesPerTrade { get; set; }

    [Precision(28, 10)]
    public decimal MaxTrailingDrawdown { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<MlTrainingRun> TrainingRuns { get; set; } = [];

    public ICollection<TradeBot> TradeBots { get; set; } = [];
}
