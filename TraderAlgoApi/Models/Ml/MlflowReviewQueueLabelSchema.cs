using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (queue_id, schema_id) — configure with HasKey() in DbContext.</summary>
[Table("review_queue_label_schemas", Schema = "mlflow")]
public sealed class MlflowReviewQueueLabelSchema
{
    [Column("queue_id")]
    public string QueueId { get; set; } = null!;

    [Column("schema_id")]
    public string SchemaId { get; set; } = null!;
}
