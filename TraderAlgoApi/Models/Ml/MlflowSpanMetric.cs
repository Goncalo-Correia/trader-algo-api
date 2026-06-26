using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (trace_id, span_id, key) — configure with HasKey() in DbContext.</summary>
[Table("span_metrics", Schema = "mlflow")]
public sealed class MlflowSpanMetric
{
    [Column("trace_id")]
    public string TraceId { get; set; } = null!;

    [Column("span_id")]
    public string SpanId { get; set; } = null!;

    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public double? Value { get; set; }
}
