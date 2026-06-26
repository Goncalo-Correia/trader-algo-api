using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("experiments", Schema = "mlflow")]
public sealed class MlflowExperiment
{
    [Key]
    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("artifact_location")]
    public string? ArtifactLocation { get; set; }

    [Column("lifecycle_stage")]
    public string? LifecycleStage { get; set; }

    [Column("creation_time")]
    public long? CreationTime { get; set; }

    [Column("last_update_time")]
    public long? LastUpdateTime { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
