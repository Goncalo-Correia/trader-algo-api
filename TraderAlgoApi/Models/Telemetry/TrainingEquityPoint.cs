using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>One equity/drawdown sample of a split's equity curve.</summary>
[Table("training_equity_points")]
public sealed class TrainingEquityPoint
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("split")]
    public string? Split { get; set; }

    [Column("ts")]
    public long? Ts { get; set; }

    [Column("equity")]
    public double? Equity { get; set; }

    [Column("drawdown_pct")]
    public double? DrawdownPct { get; set; }

    [Column("realized_pnl")]
    public double? RealizedPnl { get; set; }

    [Column("position")]
    public int? Position { get; set; }
}
