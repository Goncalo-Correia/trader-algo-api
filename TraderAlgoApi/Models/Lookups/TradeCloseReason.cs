using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Lookups;

[Table("trade_close_reasons")]
public sealed class TradeCloseReason
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Name { get; set; } = string.Empty;
}
