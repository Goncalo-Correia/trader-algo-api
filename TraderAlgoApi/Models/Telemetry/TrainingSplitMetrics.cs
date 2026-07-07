using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>full_report performance metrics for a single split (train/val/test/oos).</summary>
[Table("training_split_metrics")]
public sealed class TrainingSplitMetrics
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("split")]
    public string? Split { get; set; }

    [Column("total_return_pct")]
    public double? TotalReturnPct { get; set; }

    [Column("annualized_return_pct")]
    public double? AnnualizedReturnPct { get; set; }

    [Column("max_drawdown_pct")]
    public double? MaxDrawdownPct { get; set; }

    [Column("sharpe_like")]
    public double? SharpeLike { get; set; }

    [Column("sortino_ratio")]
    public double? SortinoRatio { get; set; }

    [Column("calmar_ratio")]
    public double? CalmarRatio { get; set; }

    [Column("profit_factor")]
    public double? ProfitFactor { get; set; }

    [Column("win_rate_pct")]
    public double? WinRatePct { get; set; }

    [Column("avg_r")]
    public double? AvgR { get; set; }

    [Column("n_trades")]
    public int? NTrades { get; set; }
}
