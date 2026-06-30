using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TraderAlgoApi.Models.Lookups;

namespace TraderAlgoApi.Models;

/// <summary>
/// A long-running background sync (data collection or indicator recompute), enqueued by an endpoint
/// and executed off the request thread. The row is the durable status record: it survives restarts
/// so progress can be polled and interrupted jobs can be resumed.
/// </summary>
[Table("sync_jobs")]
public sealed class SyncJob
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public int TypeId { get; set; }

    [ForeignKey(nameof(TypeId))]
    public SyncJobType Type { get; set; } = null!;

    /// <summary>Strongly-typed view over <see cref="TypeId"/>; the single int↔enum mapping point.</summary>
    [NotMapped]
    public Enums.SyncJobType TypeEnum
    {
        get => (Enums.SyncJobType)TypeId;
        set => TypeId = (int)value;
    }

    public int StatusId { get; set; }

    [ForeignKey(nameof(StatusId))]
    public SyncJobStatus Status { get; set; } = null!;

    /// <summary>Strongly-typed view over <see cref="StatusId"/>; the single int↔enum mapping point.</summary>
    [NotMapped]
    public Enums.SyncJobStatus StatusEnum
    {
        get => (Enums.SyncJobStatus)StatusId;
        set => StatusId = (int)value;
    }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Total units of work (active symbol × interval pairs) once the job starts.</summary>
    public int TotalUnits { get; set; }

    /// <summary>Units processed so far, for progress reporting.</summary>
    public int CompletedUnits { get; set; }

    /// <summary>Latest human-readable progress line (current pair, totals).</summary>
    [MaxLength(500)]
    public string? Message { get; set; }

    /// <summary>Failure detail when the job ends in <see cref="Enums.SyncJobStatus.Failed"/>.</summary>
    [MaxLength(2000)]
    public string? Error { get; set; }
}
