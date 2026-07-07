using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>Per-checkpoint train/val evaluation captured during best-OOS checkpoint selection.</summary>
[Table("training_checkpoint_evals")]
public sealed class TrainingCheckpointEval
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("timesteps")]
    public int? Timesteps { get; set; }

    [Column("train_eval_r")]
    public double? TrainEvalR { get; set; }

    [Column("val_r")]
    public double? ValR { get; set; }

    [Column("train_dd_pct")]
    public double? TrainDdPct { get; set; }

    [Column("val_dd_pct")]
    public double? ValDdPct { get; set; }

    [Column("q_train")]
    public double? QTrain { get; set; }

    [Column("q_val")]
    public double? QVal { get; set; }

    [Column("score")]
    public double? Score { get; set; }

    [Column("gap")]
    public double? Gap { get; set; }

    [Column("eligible")]
    public bool? Eligible { get; set; }

    [Column("is_best")]
    public bool? IsBest { get; set; }
}
