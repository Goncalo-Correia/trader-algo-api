using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Telemetry;

/// <summary>Reward-vs-timesteps learning-curve point streamed during training.</summary>
[Table("training_learning_curve")]
public sealed class TrainingLearningCurve
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("timesteps")]
    public int? Timesteps { get; set; }

    [Column("mean_ep_reward")]
    public double? MeanEpReward { get; set; }

    [Column("std_ep_reward")]
    public double? StdEpReward { get; set; }

    [Column("mean_ep_length")]
    public double? MeanEpLength { get; set; }
}
