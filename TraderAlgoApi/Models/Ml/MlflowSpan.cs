using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (trace_id, span_id) — configure with HasKey() in DbContext.</summary>
[Table("spans", Schema = "mlflow")]
public sealed class MlflowSpan
{
    [Column("trace_id")]
    public string TraceId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("span_id")]
    public string SpanId { get; set; } = null!;

    [Column("parent_span_id")]
    public string? ParentSpanId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("start_time_unix_nano")]
    public long StartTimeUnixNano { get; set; }

    [Column("end_time_unix_nano")]
    public long? EndTimeUnixNano { get; set; }

    [Column("duration_ns")]
    public long? DurationNs { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("dimension_attributes")]
    public string? DimensionAttributes { get; set; }
}
