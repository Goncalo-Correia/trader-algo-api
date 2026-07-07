using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>One telemetry trade row (drives R-dist / monthly / exit / bracket charts).</summary>
[Table("training_trades")]
public sealed class TrainingTrade
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("split")]
    public string? Split { get; set; }

    [Column("entry_time")]
    public long? EntryTime { get; set; }

    [Column("exit_time")]
    public long? ExitTime { get; set; }

    [Column("direction")]
    public string? Direction { get; set; }

    [Column("entry_price")]
    public double? EntryPrice { get; set; }

    [Column("exit_price")]
    public double? ExitPrice { get; set; }

    [Column("sl")]
    public double? Sl { get; set; }

    [Column("tp")]
    public double? Tp { get; set; }

    [Column("sl_atr_mult")]
    public double? SlAtrMult { get; set; }

    [Column("tp_r_bracket")]
    public double? TpRBracket { get; set; }

    [Column("units")]
    public double? Units { get; set; }

    [Column("pnl")]
    public double? Pnl { get; set; }

    [Column("r_mult")]
    public double? RMult { get; set; }

    [Column("bars_in_trade")]
    public int? BarsInTrade { get; set; }

    [Column("exit_reason")]
    public string? ExitReason { get; set; }
}
