using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (endpoint_id, guardrail_id) — configure with HasKey() in DbContext.</summary>
[Table("guardrail_configs", Schema = "mlflow")]
public sealed class MlflowGuardrailConfig
{
    [Column("endpoint_id")]
    public string EndpointId { get; set; } = null!;

    [Column("guardrail_id")]
    public string GuardrailId { get; set; } = null!;

    [Column("execution_order")]
    public int? ExecutionOrder { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public long CreatedAt { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
