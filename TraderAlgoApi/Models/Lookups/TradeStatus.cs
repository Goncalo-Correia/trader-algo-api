using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Lookups;

[Table("trade_statuses")]
public sealed class TradeStatus
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Name { get; set; } = string.Empty;
}
