using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

[Table("kline_data")]
public sealed class KlineData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int SymbolId { get; set; }

    [Required]
    public int IntervalId { get; set; }

    [Required]
    public DateTimeOffset OpenTime { get; set; }

    [Required]
    public DateTimeOffset CloseTime { get; set; }

    [Precision(28, 10)]
    public decimal Open { get; set; }

    [Precision(28, 10)]
    public decimal High { get; set; }

    [Precision(28, 10)]
    public decimal Low { get; set; }

    [Precision(28, 10)]
    public decimal Close { get; set; }

    [Precision(28, 10)]
    public decimal Volume { get; set; }

    [Precision(28, 10)]
    public decimal QuoteAssetVolume { get; set; }

    public int NumberOfTrades { get; set; }

    [Precision(28, 10)]
    public decimal TakerBuyBaseAssetVolume { get; set; }

    [Precision(28, 10)]
    public decimal TakerBuyQuoteAssetVolume { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [ForeignKey(nameof(SymbolId))]
    public Symbol Symbol { get; set; } = null!;

    [ForeignKey(nameof(IntervalId))]
    public Interval Interval { get; set; } = null!;

    public SimpleMovingAverage? SimpleMovingAverage { get; set; }

    public RelativeStrengthIndex? RelativeStrengthIndex { get; set; }

    public Macd? Macd { get; set; }

    public Atr? Atr { get; set; }
}
