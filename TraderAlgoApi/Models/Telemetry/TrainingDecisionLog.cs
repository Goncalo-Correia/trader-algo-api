using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>
/// The deterministic decision log for a training run — the full-range replay (per-candle
/// decisions + trades + run summary) the Python ML sidecar produces, stored whole as one
/// <c>jsonb</c> blob keyed by <c>run_id</c>. The .NET API reads this to serve
/// GET/DELETE <c>/training-runs/{id}/decisions</c> (previously proxied to the sidecar over HTTP).
/// Lives in the <c>public</c> schema; the sidecar writes it, the .NET API reads/deletes it.
/// </summary>
[Table("training_decisions")]
public sealed class TrainingDecisionLog
{
    [Key]
    [Column("run_id")]
    public string RunId { get; set; } = null!;

    /// <summary>
    /// The full <c>MlTrainingDecisionsResponse</c> payload as JSON (snake_case keys), deserialized
    /// on read. Stored as <c>jsonb</c>; mapped to string like the other JSON telemetry columns.
    /// </summary>
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = null!;

    [Column("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}
