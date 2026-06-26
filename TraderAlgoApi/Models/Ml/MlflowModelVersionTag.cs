using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, name, version, workspace) — configure with HasKey() in DbContext.</summary>
[Table("model_version_tags", Schema = "mlflow")]
public sealed class MlflowModelVersionTag
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string? Value { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("version")]
    public int Version { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
