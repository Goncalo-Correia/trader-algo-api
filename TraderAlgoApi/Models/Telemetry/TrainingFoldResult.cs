using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>Per-fold walk-forward result (block/sliding schemes).</summary>
[Table("training_fold_results")]
public sealed class TrainingFoldResult
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("fold")]
    public int? Fold { get; set; }

    [Column("scheme")]
    public string? Scheme { get; set; }

    [Column("is_oos")]
    public bool? IsOos { get; set; }

    [Column("train_start")]
    public string? TrainStart { get; set; }

    [Column("train_end")]
    public string? TrainEnd { get; set; }

    [Column("val_start")]
    public string? ValStart { get; set; }

    [Column("val_end")]
    public string? ValEnd { get; set; }

    [Column("test_start")]
    public string? TestStart { get; set; }

    [Column("test_end")]
    public string? TestEnd { get; set; }

    [Column("return_pct")]
    public double? ReturnPct { get; set; }

    [Column("sharpe")]
    public double? Sharpe { get; set; }

    [Column("profit_factor")]
    public double? ProfitFactor { get; set; }

    [Column("win_rate_pct")]
    public double? WinRatePct { get; set; }

    [Column("max_dd_pct")]
    public double? MaxDdPct { get; set; }

    [Column("avg_r")]
    public double? AvgR { get; set; }

    [Column("n_trades")]
    public int? NTrades { get; set; }
}
