using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, name, workspace) — configure with HasKey() in DbContext.</summary>
[Table("registered_model_tags", Schema = "mlflow")]
public sealed class MlflowRegisteredModelTag
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string? Value { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
