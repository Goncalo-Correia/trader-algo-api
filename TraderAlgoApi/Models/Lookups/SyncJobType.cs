using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Lookups;

[Table("sync_job_types")]
public sealed class SyncJobType
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Name { get; set; } = string.Empty;
}
