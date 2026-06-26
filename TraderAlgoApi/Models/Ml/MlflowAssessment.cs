using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("assessments", Schema = "mlflow")]
public sealed class MlflowAssessment
{
    [Key]
    [Column("assessment_id")]
    public string AssessmentId { get; set; } = null!;

    [Column("trace_id")]
    public string TraceId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("assessment_type")]
    public string AssessmentType { get; set; } = null!;

    [Column("value")]
    public string Value { get; set; } = null!;

    [Column("error")]
    public string? Error { get; set; }

    [Column("created_timestamp")]
    public long CreatedTimestamp { get; set; }

    [Column("last_updated_timestamp")]
    public long LastUpdatedTimestamp { get; set; }

    [Column("source_type")]
    public string SourceType { get; set; } = null!;

    [Column("source_id")]
    public string? SourceId { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("span_id")]
    public string? SpanId { get; set; }

    [Column("rationale")]
    public string? Rationale { get; set; }

    [Column("overrides")]
    public string? Overrides { get; set; }

    [Column("valid")]
    public bool Valid { get; set; }

    [Column("assessment_metadata")]
    public string? AssessmentMetadata { get; set; }
}
