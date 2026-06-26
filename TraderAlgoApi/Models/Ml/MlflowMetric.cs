using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, value, timestamp, run_uuid, step, is_nan) — configure with HasKey() in DbContext.</summary>
[Table("metrics", Schema = "mlflow")]
public sealed class MlflowMetric
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public double Value { get; set; }

    [Column("timestamp")]
    public long Timestamp { get; set; }

    [Column("run_uuid")]
    public string RunUuid { get; set; } = null!;

    [Column("step")]
    public long Step { get; set; }

    [Column("is_nan")]
    public bool IsNan { get; set; }
}
