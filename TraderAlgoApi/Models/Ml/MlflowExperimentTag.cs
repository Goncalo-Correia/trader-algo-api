using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, experiment_id) — configure with HasKey() in DbContext.</summary>
[Table("experiment_tags", Schema = "mlflow")]
public sealed class MlflowExperimentTag
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string? Value { get; set; }

    [Column("experiment_id")]
    public int ExperimentId { get; set; }
}
