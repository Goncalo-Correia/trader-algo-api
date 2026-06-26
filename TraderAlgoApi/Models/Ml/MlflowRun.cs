using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("runs", Schema = "mlflow")]
public sealed class MlflowRun
{
    [Key]
    [Column("run_uuid")]
    public string RunUuid { get; set; } = null!;

    [Column("name")]
    public string? Name { get; set; }

    [Column("source_type")]
    public string? SourceType { get; set; }

    [Column("source_name")]
    public string? SourceName { get; set; }

    [Column("entry_point_name")]
    public string? EntryPointName { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("start_time")]
    public long? StartTime { get; set; }

    [Column("end_time")]
    public long? EndTime { get; set; }

    [Column("source_version")]
    public string? SourceVersion { get; set; }

    [Column("lifecycle_stage")]
    public string? LifecycleStage { get; set; }

    [Column("artifact_uri")]
    public string? ArtifactUri { get; set; }

    [Column("experiment_id")]
    public int? ExperimentId { get; set; }

    [Column("deleted_time")]
    public long? DeletedTime { get; set; }
}
