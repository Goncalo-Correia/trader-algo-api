using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (endpoint_id, resource_type, resource_id) — configure with HasKey() in DbContext.</summary>
[Table("endpoint_bindings", Schema = "mlflow")]
public sealed class MlflowEndpointBinding
{
    [Column("endpoint_id")]
    public string EndpointId { get; set; } = null!;

    [Column("resource_type")]
    public string ResourceType { get; set; } = null!;

    [Column("resource_id")]
    public string ResourceId { get; set; } = null!;

    [Column("created_at")]
    public long CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("last_updated_at")]
    public long LastUpdatedAt { get; set; }

    [Column("last_updated_by")]
    public string? LastUpdatedBy { get; set; }

    [Column("display_name")]
    public string? DisplayName { get; set; }
}
