using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, run_uuid) — configure with HasKey() in DbContext.</summary>
[Table("tags", Schema = "mlflow")]
public sealed class MlflowTag
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string? Value { get; set; }

    [Column("run_uuid")]
    public string RunUuid { get; set; } = null!;
}
