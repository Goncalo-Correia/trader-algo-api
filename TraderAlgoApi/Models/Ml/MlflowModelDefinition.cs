using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("model_definitions", Schema = "mlflow")]
public sealed class MlflowModelDefinition
{
    [Key]
    [Column("model_definition_id")]
    public string ModelDefinitionId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("secret_id")]
    public string? SecretId { get; set; }

    [Column("provider")]
    public string Provider { get; set; } = null!;

    [Column("model_name")]
    public string ModelName { get; set; } = null!;

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
