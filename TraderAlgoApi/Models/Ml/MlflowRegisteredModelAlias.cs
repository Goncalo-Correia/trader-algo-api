using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (alias, name, workspace) — configure with HasKey() in DbContext.</summary>
[Table("registered_model_aliases", Schema = "mlflow")]
public sealed class MlflowRegisteredModelAlias
{
    [Column("alias")]
    public string Alias { get; set; } = null!;

    [Column("version")]
    public int Version { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
