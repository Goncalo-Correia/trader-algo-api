using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("budget_policies", Schema = "mlflow")]
public sealed class MlflowBudgetPolicy
{
    [Key]
    [Column("budget_policy_id")]
    public string BudgetPolicyId { get; set; } = null!;

    [Column("budget_unit")]
    public string BudgetUnit { get; set; } = null!;

    [Column("budget_amount")]
    public double BudgetAmount { get; set; }

    [Column("duration_unit")]
    public string DurationUnit { get; set; } = null!;

    [Column("duration_value")]
    public int DurationValue { get; set; }

    [Column("target_scope")]
    public string TargetScope { get; set; } = null!;

    [Column("budget_action")]
    public string BudgetAction { get; set; } = null!;

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
