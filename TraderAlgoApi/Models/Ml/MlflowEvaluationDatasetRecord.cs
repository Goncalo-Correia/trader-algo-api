using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("evaluation_dataset_records", Schema = "mlflow")]
public sealed class MlflowEvaluationDatasetRecord
{
    [Key]
    [Column("dataset_record_id")]
    public string DatasetRecordId { get; set; } = null!;

    [Column("dataset_id")]
    public string DatasetId { get; set; } = null!;

    [Column("inputs")]
    public string Inputs { get; set; } = null!;

    [Column("expectations")]
    public string? Expectations { get; set; }

    [Column("tags")]
    public string? Tags { get; set; }

    [Column("source")]
    public string? Source { get; set; }

    [Column("source_id")]
    public string? SourceId { get; set; }

    [Column("source_type")]
    public string? SourceType { get; set; }

    [Column("created_time")]
    public long? CreatedTime { get; set; }

    [Column("last_update_time")]
    public long? LastUpdateTime { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("last_updated_by")]
    public string? LastUpdatedBy { get; set; }

    [Column("input_hash")]
    public string InputHash { get; set; } = null!;

    [Column("outputs")]
    public string? Outputs { get; set; }
}
