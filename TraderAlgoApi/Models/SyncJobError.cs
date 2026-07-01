using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models;

/// <summary>
/// A durable record of a single candle that failed to collect during a <see cref="SyncJob"/>. The
/// data collector surfaces failures on its result; the job executor stamps each one with the owning
/// job and writes it here so failures can be inspected/verified per job afterwards. Symbol and
/// interval are stored as codes (denormalized) so the row stays self-contained and readable.
/// </summary>
[Table("sync_job_errors")]
public sealed class SyncJobError
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>The sync job during which this error occurred.</summary>
    public long SyncJobId { get; set; }

    [ForeignKey(nameof(SyncJobId))]
    public SyncJob SyncJob { get; set; } = null!;

    /// <summary>Code of the symbol being collected when the error occurred.</summary>
    [Required]
    [MaxLength(50)]
    public string Symbol { get; set; } = null!;

    /// <summary>Code of the interval being collected when the error occurred.</summary>
    [Required]
    [MaxLength(50)]
    public string Interval { get; set; } = null!;

    /// <summary>
    /// Open time of the candle the error refers to, or <see langword="null"/> when the failure could
    /// not be tied to a single candle (e.g. a batch persist covering several candles).
    /// </summary>
    public DateTimeOffset? CandleOpenTime { get; set; }

    /// <summary>Human-readable description of the failure.</summary>
    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
