using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (scorer_id, scorer_version) — configure with HasKey() in DbContext.</summary>
[Table("scorer_versions", Schema = "mlflow")]
public sealed class MlflowScorerVersion
{
    [Column("scorer_id")]
    public string ScorerId { get; set; } = null!;

    [Column("scorer_version")]
    public int ScorerVersion { get; set; }

    [Column("serialized_scorer")]
    public string SerializedScorer { get; set; } = null!;

    [Column("creation_time")]
    public long? CreationTime { get; set; }
}
