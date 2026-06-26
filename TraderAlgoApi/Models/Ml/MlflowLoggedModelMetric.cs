using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (model_id, metric_name, metric_timestamp_ms, metric_step, run_id) — configure with HasKey() in DbContext.</summary>
[Table("logged_model_metrics", Schema = "mlflow")]
public sealed class MlflowLoggedModelMetric
{
    [Column("model_id")]
    public string ModelId { get; set; } = null!;

    [Column("metric_name")]
    public string MetricName { get; set; } = null!;

    [Column("metric_timestamp_ms")]
    public long MetricTimestampMs { get; set; }

    [Column("metric_step")]
    public long MetricStep { get; set; }

    [Column("metric_value")]
    public double? MetricValue { get; set; }

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("run_id")]
    public string RunId { get; set; } = null!;

    [Column("dataset_uuid")]
    public string? DatasetUuid { get; set; }

    [Column("dataset_name")]
    public string? DatasetName { get; set; }

    [Column("dataset_digest")]
    public string? DatasetDigest { get; set; }
}
