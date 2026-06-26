using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (experiment_id, name, digest) — configure with HasKey() in DbContext.</summary>
[Table("datasets", Schema = "mlflow")]
public sealed class MlflowDataset
{
    [Column("dataset_uuid")]
    public string DatasetUuid { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("digest")]
    public string Digest { get; set; } = null!;

    [Column("dataset_source_type")]
    public string DatasetSourceType { get; set; } = null!;

    [Column("dataset_source")]
    public string DatasetSource { get; set; } = null!;

    [Column("dataset_schema")]
    public string? DatasetSchema { get; set; }

    [Column("dataset_profile")]
    public string? DatasetProfile { get; set; }
}
