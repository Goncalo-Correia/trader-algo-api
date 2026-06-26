using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (queue_id, user_id) — configure with HasKey() in DbContext.</summary>
[Table("review_queue_users", Schema = "mlflow")]
public sealed class MlflowReviewQueueUser
{
    [Column("queue_id")]
    public string QueueId { get; set; } = null!;

    [Column("user_id")]
    public string UserId { get; set; } = null!;
}
