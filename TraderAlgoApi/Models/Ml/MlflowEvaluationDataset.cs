using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("evaluation_datasets", Schema = "mlflow")]
public sealed class MlflowEvaluationDataset
{
    [Key]
    [Column("dataset_id")]
    public string DatasetId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("schema")]
    public string? Schema { get; set; }

    [Column("profile")]
    public string? Profile { get; set; }

    [Column("digest")]
    public string? Digest { get; set; }

    [Column("created_time")]
    public long? CreatedTime { get; set; }

    [Column("last_update_time")]
    public long? LastUpdateTime { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("last_updated_by")]
    public string? LastUpdatedBy { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
