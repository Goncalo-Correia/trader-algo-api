using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>
/// Denormalised per-run performance summary written by the Python ML sidecar's telemetry sink.
/// Lives in the <c>public</c> schema (columns are snake_case). The .NET API only reads these rows.
/// </summary>
[Table("training_run_performance")]
public sealed class TrainingRunPerformance
{
    [Key]
    [Column("run_id")]
    public string RunId { get; set; } = null!;

    [Column("ml_policy_id")]
    public int? MlPolicyId { get; set; }

    [Column("scheme")]
    public string? Scheme { get; set; }

    [Column("from_date")]
    public string? FromDate { get; set; }

    [Column("to_date")]
    public string? ToDate { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("promoted")]
    public bool? Promoted { get; set; }

    [Column("gate_passed")]
    public bool? GatePassed { get; set; }

    [Column("gate_detail", TypeName = "jsonb")]
    public string? GateDetail { get; set; }

    [Column("seed")]
    public int? Seed { get; set; }

    [Column("obs_dim")]
    public int? ObsDim { get; set; }

    [Column("schema_version")]
    public int? SchemaVersion { get; set; }

    [Column("in_sample_pnl_pct")]
    public double? InSamplePnlPct { get; set; }

    [Column("oos_pnl_pct")]
    public double? OosPnlPct { get; set; }

    [Column("oos_sharpe")]
    public double? OosSharpe { get; set; }

    [Column("oos_profit_factor")]
    public double? OosProfitFactor { get; set; }

    [Column("oos_max_dd_pct")]
    public double? OosMaxDdPct { get; set; }

    [Column("in_sample_minus_oos_pnl_pct")]
    public double? InSampleMinusOosPnlPct { get; set; }

    [Column("n_folds")]
    public int? NFolds { get; set; }

    [Column("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}
