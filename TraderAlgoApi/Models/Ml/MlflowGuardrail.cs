using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("guardrails", Schema = "mlflow")]
public sealed class MlflowGuardrail
{
    [Key]
    [Column("guardrail_id")]
    public string GuardrailId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("scorer_id")]
    public string ScorerId { get; set; } = null!;

    [Column("scorer_version")]
    public int ScorerVersion { get; set; }

    [Column("stage")]
    public string Stage { get; set; } = null!;

    [Column("action")]
    public string Action { get; set; } = null!;

    [Column("action_endpoint_id")]
    public string? ActionEndpointId { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public long CreatedAt { get; set; }

    [Column("last_updated_by")]
    public string? LastUpdatedBy { get; set; }

    [Column("last_updated_at")]
    public long LastUpdatedAt { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
