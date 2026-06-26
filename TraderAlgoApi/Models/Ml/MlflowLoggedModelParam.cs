using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (model_id, param_key) — configure with HasKey() in DbContext.</summary>
[Table("logged_model_params", Schema = "mlflow")]
public sealed class MlflowLoggedModelParam
{
    [Column("model_id")]
    public string ModelId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("param_key")]
    public string ParamKey { get; set; } = null!;

    [Column("param_value")]
    public string ParamValue { get; set; } = null!;
}
