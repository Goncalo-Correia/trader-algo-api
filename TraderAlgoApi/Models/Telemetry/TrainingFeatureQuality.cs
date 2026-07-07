using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>Per-feature stationarity / 1-bar Spearman signal quality diagnostics.</summary>
[Table("training_feature_quality")]
public sealed class TrainingFeatureQuality
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("feature")]
    public string? Feature { get; set; }

    [Column("mean")]
    public double? Mean { get; set; }

    [Column("std")]
    public double? Std { get; set; }

    [Column("skew")]
    public double? Skew { get; set; }

    [Column("excess_kurt")]
    public double? ExcessKurt { get; set; }

    [Column("cv")]
    public double? Cv { get; set; }

    [Column("spearman_r_1bar")]
    public double? SpearmanR1Bar { get; set; }

    [Column("spearman_p_1bar")]
    public double? SpearmanP1Bar { get; set; }

    [Column("signal_p05")]
    public bool? SignalP05 { get; set; }
}
