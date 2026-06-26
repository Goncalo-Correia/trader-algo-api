using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("workspaces", Schema = "mlflow")]
public sealed class MlflowWorkspace
{
    [Key]
    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("default_artifact_root")]
    public string? DefaultArtifactRoot { get; set; }

    [Column("trace_archival_location")]
    public string? TraceArchivalLocation { get; set; }

    [Column("trace_archival_retention")]
    public string? TraceArchivalRetention { get; set; }
}
