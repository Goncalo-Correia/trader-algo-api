using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("jobs", Schema = "mlflow")]
public sealed class MlflowJob
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = null!;

    [Column("creation_time")]
    public long CreationTime { get; set; }

    [Column("job_name")]
    public string JobName { get; set; } = null!;

    [Column("params")]
    public string Params { get; set; } = null!;

    [Column("timeout")]
    public double? Timeout { get; set; }

    [Column("status")]
    public int Status { get; set; }

    [Column("result")]
    public string? Result { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("last_update_time")]
    public long LastUpdateTime { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;

    [Column("status_details")]
    public string? StatusDetails { get; set; }
}
