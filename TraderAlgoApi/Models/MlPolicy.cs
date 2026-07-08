using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

/// <summary>
/// A reusable training configuration: which model to train on which symbol/interval, plus the
/// risk/environment parameters forwarded to the ML train endpoint. One policy can have many
/// <see cref="MlTrainingRun"/>s (each run trains this policy over a chosen date range). Cash risk
/// params are absolute amounts (matching backtests); <see cref="Breakeven"/>/<see cref="BreakevenStop"/>
/// are ATR multipliers (see their docs) used by the bound bot's live/backtest execution. The
/// stop-loss and take-profit brackets are chosen by the model at entry, so they are not stored here.
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

    /// <summary>
    /// Breakeven trigger as an ATR multiplier evaluated against ATR at entry: the stop is moved to
    /// breakeven once price reaches entry ± (Breakeven × ATR_at_entry). Not an absolute price offset.
    /// 0 disables the breakeven ratchet. Typical range ~0.5–2.0.
    /// </summary>
    [Precision(28, 10)]
    public decimal Breakeven { get; set; }

    /// <summary>
    /// Ratcheted stop as an ATR multiplier evaluated against ATR at entry: once breakeven triggers,
    /// the stop moves to entry ± (BreakevenStop × ATR_at_entry). Not an absolute price offset.
    /// Typical range ~0.0–1.0.
    /// </summary>
    [Precision(28, 10)]
    public decimal BreakevenStop { get; set; }

    [Precision(28, 10)]
    public decimal Fee { get; set; }

    /// <summary>
    /// Per-fill slippage as an <b>ATR fraction</b>: the ML backtest/env applies a price offset of
    /// Slippage × ATR-at-entry on each entry and exit fill (not a fixed price offset).
    /// </summary>
    [Precision(28, 10)]
    public decimal Slippage { get; set; }

    /// <summary>
    /// Optional cash risked at the stop for volatility-targeted position sizing. When set (&gt; 0),
    /// the ML position size is RiskPerTrade / stop_distance (stop_distance = sl_atr_mult ×
    /// ATR-at-entry). Null/≤ 0 falls back to the fixed <see cref="Quantity"/>. ML-specific.
    /// </summary>
    [Precision(28, 10)]
    public decimal? RiskPerTrade { get; set; }

    [Precision(28, 10)]
    public decimal DailyProfit { get; set; }

    [Precision(28, 10)]
    public decimal DailyDrawdownLimit { get; set; }

    public int MaxCandlesPerTrade { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<MlTrainingRun> TrainingRuns { get; set; } = [];

    public ICollection<TradeBot> TradeBots { get; set; } = [];
}
