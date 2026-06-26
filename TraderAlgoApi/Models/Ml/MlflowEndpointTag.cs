using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, endpoint_id) — configure with HasKey() in DbContext.</summary>
[Table("endpoint_tags", Schema = "mlflow")]
public sealed class MlflowEndpointTag
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string? Value { get; set; }

    [Column("endpoint_id")]
    public string EndpointId { get; set; } = null!;
}
