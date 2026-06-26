using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("label_schemas", Schema = "mlflow")]
public sealed class MlflowLabelSchema
{
    [Key]
    [Column("schema_id")]
    public string SchemaId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("type")]
    public string Type { get; set; } = null!;

    [Column("instruction")]
    public string? Instruction { get; set; }

    [Column("enable_comment")]
    public bool EnableComment { get; set; }

    [Column("input_type")]
    public string InputType { get; set; } = null!;

    [Column("input_config")]
    public string InputConfig { get; set; } = null!;

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_time")]
    public long CreatedTime { get; set; }

    [Column("last_update_time")]
    public long LastUpdateTime { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; }
}
