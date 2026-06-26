using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("online_scoring_configs", Schema = "mlflow")]
public sealed class MlflowOnlineScoringConfig
{
    [Key]
    [Column("online_scoring_config_id")]
    public string OnlineScoringConfigId { get; set; } = null!;

    [Column("scorer_id")]
    public string ScorerId { get; set; } = null!;

    [Column("sample_rate")]
    public double SampleRate { get; set; }

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("filter_string")]
    public string? FilterString { get; set; }
}
