using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Lookups;

namespace TraderAlgoApi.Models;

/// <summary>
/// One execution of an <see cref="MlPolicy"/> over a date range. Model / symbol / interval /
/// hyperparameters all come from the linked policy; this row holds only run-specific state:
/// the date range, status, and final metrics.
/// </summary>
[Table("ml_training_runs")]
public sealed class MlTrainingRun
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long MlPolicyId { get; set; }

    [ForeignKey(nameof(MlPolicyId))]
    public MlPolicy Policy { get; set; } = null!;

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int StatusId { get; set; }

    [ForeignKey(nameof(StatusId))]
    public MlTrainingRunStatus Status { get; set; } = null!;

    /// <summary>
    /// Strongly-typed view over <see cref="StatusId"/>. Lookup IDs match the enum values, so this is
    /// the single place the int↔enum mapping lives. Not mapped: persistence goes through StatusId.
    /// </summary>
    [NotMapped]
    public Enums.MlTrainingRunStatus StatusEnum
    {
        get => (Enums.MlTrainingRunStatus)StatusId;
        set => StatusId = (int)value;
    }

    [Precision(28, 10)]
    public decimal? FinalBalance { get; set; }

    [Precision(28, 10)]
    public decimal? PnlPct { get; set; }

    public int? NTrades { get; set; }

    /// <summary>The Python-side run identifier (MLflow run), filled on completion.</summary>
    [MaxLength(50)]
    public string? RunId { get; set; }
}
