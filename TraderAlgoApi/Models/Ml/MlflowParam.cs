using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, run_uuid) — configure with HasKey() in DbContext.</summary>
[Table("params", Schema = "mlflow")]
public sealed class MlflowParam
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string Value { get; set; } = null!;

    [Column("run_uuid")]
    public string RunUuid { get; set; } = null!;
}
