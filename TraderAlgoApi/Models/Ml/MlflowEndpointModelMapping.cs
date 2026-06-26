using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("endpoint_model_mappings", Schema = "mlflow")]
public sealed class MlflowEndpointModelMapping
{
    [Key]
    [Column("mapping_id")]
    public string MappingId { get; set; } = null!;

    [Column("endpoint_id")]
    public string EndpointId { get; set; } = null!;

    [Column("model_definition_id")]
    public string ModelDefinitionId { get; set; } = null!;

    [Column("weight")]
    public double Weight { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public long CreatedAt { get; set; }

    [Column("linkage_type")]
    public string LinkageType { get; set; } = null!;

    [Column("fallback_order")]
    public int? FallbackOrder { get; set; }
}
