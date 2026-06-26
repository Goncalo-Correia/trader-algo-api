using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (queue_id, item_id) — configure with HasKey() in DbContext.</summary>
[Table("review_queue_items", Schema = "mlflow")]
public sealed class MlflowReviewQueueItem
{
    [Column("queue_id")]
    public string QueueId { get; set; } = null!;

    [Column("item_type")]
    public string ItemType { get; set; } = null!;

    [Column("item_id")]
    public string ItemId { get; set; } = null!;

    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("completed_by")]
    public string? CompletedBy { get; set; }

    [Column("completed_time_ms")]
    public long? CompletedTimeMs { get; set; }

    [Column("creation_time_ms")]
    public long CreationTimeMs { get; set; }

    [Column("last_update_time_ms")]
    public long LastUpdateTimeMs { get; set; }
}
