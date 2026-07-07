using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

/// <summary>
/// A reusable training configuration: which model to train on which symbol/interval, plus all
/// PPO/risk hyperparameters. One policy can have many <see cref="MlTrainingRun"/>s (each run trains
/// this policy over a chosen date range). Cash risk params are absolute amounts (matching backtests);
/// <see cref="Breakeven"/>/<see cref="BreakevenStop"/> are ATR multipliers (see their docs). The
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

    [Precision(28, 10)]
    public decimal Slippage { get; set; }

    [Precision(28, 10)]
    public decimal DailyProfit { get; set; }

    [Precision(28, 10)]
    public decimal DailyDrawdownLimit { get; set; }

    public int MaxCandlesPerTrade { get; set; }

    // ---------------------------------------------------------------------
    // Optional ML training tuning parameters (§3). All nullable: when null the
    // parameter is omitted from the /train request and the ML service applies its
    // own default, preserving prior behavior.
    // ---------------------------------------------------------------------

    /// <summary>Length of each randomized training window in days (ML default 5.0).</summary>
    public double? EpisodeDays { get; set; }

    /// <summary>Reward penalty per trade entry (ML default 0.05).</summary>
    public double? EntryCost { get; set; }

    /// <summary>Flat reward penalty per day with no trades (ML default 1.0).</summary>
    public double? NoTradeDayPenalty { get; set; }

    /// <summary>Win/loss streak reward coefficient (ML default 0.1).</summary>
    public double? StreakBonusCoef { get; set; }

    /// <summary>Cap on the streak reward (ML default 0.5).</summary>
    public double? MaxStreakBonus { get; set; }

    /// <summary>Cap on the "sit out low-quality candles" reward (ML default 0.5).</summary>
    public double? MaxPatienceRewardPerDay { get; set; }

    /// <summary>PPO learning rate (ML default 0.0003).</summary>
    public double? LearningRate { get; set; }

    /// <summary>Rollout length, fresh models only (ML default 2048).</summary>
    public int? NSteps { get; set; }

    /// <summary>PPO batch size, fresh models only (ML default 64).</summary>
    public int? BatchSize { get; set; }

    /// <summary>PPO epochs per update (ML default 10).</summary>
    public int? NEpochs { get; set; }

    /// <summary>Discount factor (ML default 0.99).</summary>
    public double? Gamma { get; set; }

    /// <summary>GAE lambda (ML default 0.95).</summary>
    public double? GaeLambda { get; set; }

    /// <summary>PPO clip range (ML default 0.2).</summary>
    public double? ClipRange { get; set; }

    /// <summary>Entropy coefficient (ML default 0.01).</summary>
    public double? EntCoef { get; set; }

    /// <summary>Evaluate the OOS window every N rollouts; higher = faster training (ML default 1).</summary>
    public int? OosEvalEvery { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<MlTrainingRun> TrainingRuns { get; set; } = [];

    public ICollection<TradeBot> TradeBots { get; set; } = [];
}
