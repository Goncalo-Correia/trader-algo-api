using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (webhook_id, entity, action) — configure with HasKey() in DbContext.</summary>
[Table("webhook_events", Schema = "mlflow")]
public sealed class MlflowWebhookEvent
{
    [Column("webhook_id")]
    public string WebhookId { get; set; } = null!;

    [Column("entity")]
    public string Entity { get; set; } = null!;

    [Column("action")]
    public string Action { get; set; } = null!;
}
