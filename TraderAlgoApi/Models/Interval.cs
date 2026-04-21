using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models;

[Table("intervals")]
public sealed class Interval
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public TimeSpan Duration { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<KlineData> Klines { get; set; } = [];
}
