using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("issues", Schema = "mlflow")]
public sealed class MlflowIssue
{
    [Key]
    [Column("issue_id")]
    public string IssueId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("severity")]
    public string? Severity { get; set; }

    [Column("root_causes")]
    public string? RootCauses { get; set; }

    [Column("source_run_id")]
    public string? SourceRunId { get; set; }

    [Column("categories")]
    public string? Categories { get; set; }

    [Column("created_timestamp")]
    public long CreatedTimestamp { get; set; }

    [Column("last_updated_timestamp")]
    public long LastUpdatedTimestamp { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }
}
