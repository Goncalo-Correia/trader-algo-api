using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (dataset_id, key) — configure with HasKey() in DbContext.</summary>
[Table("evaluation_dataset_tags", Schema = "mlflow")]
public sealed class MlflowEvaluationDatasetTag
{
    [Column("dataset_id")]
    public string DatasetId { get; set; } = null!;

    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string? Value { get; set; }
}
