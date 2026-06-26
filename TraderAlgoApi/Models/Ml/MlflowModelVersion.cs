using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (name, version, workspace) — configure with HasKey() in DbContext.</summary>
[Table("model_versions", Schema = "mlflow")]
public sealed class MlflowModelVersion
{
    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("version")]
    public int Version { get; set; }

    [Column("creation_time")]
    public long? CreationTime { get; set; }

    [Column("last_updated_time")]
    public long? LastUpdatedTime { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("current_stage")]
    public string? CurrentStage { get; set; }

    [Column("source")]
    public string? Source { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("status_message")]
    public string? StatusMessage { get; set; }

    [Column("run_link")]
    public string? RunLink { get; set; }

    [Column("storage_location")]
    public string? StorageLocation { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
