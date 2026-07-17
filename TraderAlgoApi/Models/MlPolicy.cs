using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

/// <summary>
/// A reusable training configuration: which model to train on which symbol/interval, plus the
/// risk/environment parameters forwarded to the ML train endpoint. One policy can have many
/// <see cref="MlTrainingRun"/>s (each run trains this policy over a chosen date range). Cash risk
/// params are absolute amounts (matching backtests). ML sizing is driven solely by
/// <see cref="RiskPerTrade"/> (volatility-targeted), and the stop-loss/take-profit brackets are
/// chosen by the model at entry, so neither is stored here.
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
    public decimal Fee { get; set; }

    /// <summary>
    /// Per-fill slippage as an <b>ATR fraction</b>: the ML backtest/env applies a price offset of
    /// Slippage × ATR-at-entry on each entry and exit fill (not a fixed price offset).
    /// </summary>
    [Precision(28, 10)]
    public decimal Slippage { get; set; }

    /// <summary>
    /// Cash risked at the stop for volatility-targeted position sizing — the sole ML sizing input.
    /// When set (&gt; 0), the ML position size is RiskPerTrade / stop_distance (stop_distance =
    /// sl_atr_mult × ATR-at-entry). Null/≤ 0 yields no size (bound ML bots require it). ML-specific.
    /// </summary>
    [Precision(28, 10)]
    public decimal? RiskPerTrade { get; set; }

    [Precision(28, 10)]
    public decimal DailyProfit { get; set; }

    [Precision(28, 10)]
    public decimal DailyDrawdownLimit { get; set; }

    public int MaxCandlesPerTrade { get; set; }

    /// <summary>
    /// High-level validation scheme forwarded to the ML <c>/train</c> endpoint as
    /// <c>validation_scheme</c>: <c>single</c> (default) or <c>block</c> (block walk-forward).
    /// Persisted as the exact lowercase string the sidecar accepts; fold/window knobs remain
    /// engine-owned. Legacy <c>sliding</c> rows normalize to <c>block</c>. See <see cref="ValidationSchemes"/>.
    /// </summary>
    [MaxLength(16)]
    public string ValidationScheme { get; set; } = ValidationSchemes.Single;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<MlTrainingRun> TrainingRuns { get; set; } = [];

    public ICollection<TradeBot> TradeBots { get; set; } = [];
}
