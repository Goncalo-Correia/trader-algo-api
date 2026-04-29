using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

[Table("simple_moving_averages")]
public sealed class SimpleMovingAverage
{
    // KlineDataId is both the primary key and the foreign key — one-to-one with KlineData.
    public long KlineDataId { get; set; }

    [Precision(28, 10)]
    [Column("sma_20")]
    public decimal? Sma20 { get; set; }

    [Precision(28, 10)]
    [Column("sma_100")]
    public decimal? Sma100 { get; set; }

    public KlineData KlineData { get; set; } = null!;
}
