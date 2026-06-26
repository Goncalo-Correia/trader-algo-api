using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (key, request_id) — configure with HasKey() in DbContext.</summary>
[Table("trace_request_metadata", Schema = "mlflow")]
public sealed class MlflowTraceRequestMetadata
{
    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string? Value { get; set; }

    [Column("request_id")]
    public string RequestId { get; set; } = null!;
}
