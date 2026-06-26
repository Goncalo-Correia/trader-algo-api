using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("scorers", Schema = "mlflow")]
public sealed class MlflowScorer
{
    [Key]
    [Column("scorer_id")]
    public string ScorerId { get; set; } = null!;

    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("scorer_name")]
    public string ScorerName { get; set; } = null!;
}
