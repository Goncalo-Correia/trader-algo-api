using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models;

/// <summary>
/// Registry of trainable/servable models. The <see cref="Name"/> matches the model id the
/// Python ML service uses on disk (e.g. "ppo-v1").
/// </summary>
[Table("ml_models")]
public sealed class MlModel
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
