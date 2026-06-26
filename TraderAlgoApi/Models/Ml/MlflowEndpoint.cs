using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("endpoints", Schema = "mlflow")]
public sealed class MlflowEndpoint
{
    [Key]
    [Column("endpoint_id")]
    public string EndpointId { get; set; } = null!;

    [Column("name")]
    public string? Name { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public long CreatedAt { get; set; }

    [Column("last_updated_by")]
    public string? LastUpdatedBy { get; set; }

    [Column("last_updated_at")]
    public long LastUpdatedAt { get; set; }

    [Column("routing_strategy")]
    public string? RoutingStrategy { get; set; }

    [Column("fallback_config_json")]
    public string? FallbackConfigJson { get; set; }

    [Column("experiment_id")]
    public int? ExperimentId { get; set; }

    [Column("usage_tracking")]
    public bool UsageTracking { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
