using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (name, workspace) — configure with HasKey() in DbContext.</summary>
[Table("registered_models", Schema = "mlflow")]
public sealed class MlflowRegisteredModel
{
    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("creation_time")]
    public long? CreationTime { get; set; }

    [Column("last_updated_time")]
    public long? LastUpdatedTime { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
