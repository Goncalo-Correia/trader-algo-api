using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("review_queues", Schema = "mlflow")]
public sealed class MlflowReviewQueue
{
    [Key]
    [Column("queue_id")]
    public string QueueId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("queue_type")]
    public string QueueType { get; set; } = null!;

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("creation_time_ms")]
    public long CreationTimeMs { get; set; }

    [Column("last_update_time_ms")]
    public long LastUpdateTimeMs { get; set; }

    [Column("name_key")]
    public string NameKey { get; set; } = null!;
}
