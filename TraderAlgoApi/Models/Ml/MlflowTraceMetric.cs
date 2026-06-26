using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (request_id, key) — configure with HasKey() in DbContext.</summary>
[Table("trace_metrics", Schema = "mlflow")]
public sealed class MlflowTraceMetric
{
    [Column("request_id")]
    public string RequestId { get; set; } = null!;

    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public double? Value { get; set; }
}
