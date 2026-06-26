using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("trace_info", Schema = "mlflow")]
public sealed class MlflowTraceInfo
{
    [Key]
    [Column("request_id")]
    public string RequestId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("timestamp_ms")]
    public long TimestampMs { get; set; }

    [Column("execution_time_ms")]
    public long? ExecutionTimeMs { get; set; }

    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("client_request_id")]
    public string? ClientRequestId { get; set; }

    [Column("request_preview")]
    public string? RequestPreview { get; set; }

    [Column("response_preview")]
    public string? ResponsePreview { get; set; }

    [Column("db_payload_generation")]
    public int DbPayloadGeneration { get; set; }
}
