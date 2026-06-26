using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (source_type, source_id, destination_type, destination_id) — configure with HasKey() in DbContext.</summary>
[Table("inputs", Schema = "mlflow")]
public sealed class MlflowInput
{
    [Column("input_uuid")]
    public string InputUuid { get; set; } = null!;

    [Column("source_type")]
    public string SourceType { get; set; } = null!;

    [Column("source_id")]
    public string SourceId { get; set; } = null!;

    [Column("destination_type")]
    public string DestinationType { get; set; } = null!;

    [Column("destination_id")]
    public string DestinationId { get; set; } = null!;

    [Column("step")]
    public long Step { get; set; }
}
