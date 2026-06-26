using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (model_id, tag_key) — configure with HasKey() in DbContext.</summary>
[Table("logged_model_tags", Schema = "mlflow")]
public sealed class MlflowLoggedModelTag
{
    [Column("model_id")]
    public string ModelId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("tag_key")]
    public string TagKey { get; set; } = null!;

    [Column("tag_value")]
    public string TagValue { get; set; } = null!;
}
