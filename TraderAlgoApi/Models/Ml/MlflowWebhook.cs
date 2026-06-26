using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("webhooks", Schema = "mlflow")]
public sealed class MlflowWebhook
{
    [Key]
    [Column("webhook_id")]
    public string WebhookId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("secret")]
    public string? Secret { get; set; }

    [Column("creation_timestamp")]
    public long? CreationTimestamp { get; set; }

    [Column("last_updated_timestamp")]
    public long? LastUpdatedTimestamp { get; set; }

    [Column("deleted_timestamp")]
    public long? DeletedTimestamp { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
