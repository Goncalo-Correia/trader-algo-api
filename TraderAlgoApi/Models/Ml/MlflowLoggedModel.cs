using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("logged_models", Schema = "mlflow")]
public sealed class MlflowLoggedModel
{
    [Key]
    [Column("model_id")]
    public string ModelId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("artifact_location")]
    public string ArtifactLocation { get; set; } = null!;

    [Column("creation_timestamp_ms")]
    public long CreationTimestampMs { get; set; }

    [Column("last_updated_timestamp_ms")]
    public long LastUpdatedTimestampMs { get; set; }

    [Column("status")]
    public int Status { get; set; }

    [Column("lifecycle_stage")]
    public string? LifecycleStage { get; set; }

    [Column("model_type")]
    public string? ModelType { get; set; }

    [Column("source_run_id")]
    public string? SourceRunId { get; set; }

    [Column("status_message")]
    public string? StatusMessage { get; set; }
}
