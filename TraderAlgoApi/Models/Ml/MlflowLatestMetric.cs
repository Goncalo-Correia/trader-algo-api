using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, run_uuid) — configure with HasKey() in DbContext.</summary>
[Table("latest_metrics", Schema = "mlflow")]
public sealed class MlflowLatestMetric
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public double Value { get; set; }

    [Column("timestamp")]
    public long? Timestamp { get; set; }

    [Column("step")]
    public long Step { get; set; }

    [Column("is_nan")]
    public bool IsNan { get; set; }

    [Column("run_uuid")]
    public string RunUuid { get; set; } = null!;
}
