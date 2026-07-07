using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>
/// Pre-rendered chart artifact stored in Supabase Storage. Composite key (run_id, chart_key).
/// Key configured via fluent API in <see cref="Data.ApplicationDbContext"/>.
/// </summary>
[Table("training_chart_artifacts")]
public sealed class TrainingChartArtifact
{
    [Column("run_id")]
    public string RunId { get; set; } = null!;

    [Column("chart_key")]
    public string ChartKey { get; set; } = null!;

    [Column("kind")]
    public string? Kind { get; set; }

    [Column("storage_path")]
    public string? StoragePath { get; set; }

    [Column("content_type")]
    public string? ContentType { get; set; }

    [Column("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}
