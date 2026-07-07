using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

[Table("atr")]
public sealed class Atr
{
    // KlineDataId is both the primary key and the foreign key — one-to-one with KlineData.
    public long KlineDataId { get; set; }

    /// <summary>Averaging period used for the ATR (Wilder's smoothing, 14 by default).</summary>
    [Column("period")]
    public int Period { get; set; }

    /// <summary>
    /// True range of this candle: max(high−low, |high−prevClose|, |low−prevClose|).
    /// The first candle has no prior close, so its true range is simply high−low.
    /// </summary>
    [Precision(28, 10)]
    [Column("true_range")]
    public decimal? TrueRange { get; set; }

    /// <summary>
    /// Average true range (Wilder's smoothing). Null for the first 13 candles (insufficient history).
    /// Named <c>AtrValue</c> rather than <c>Atr</c> because a member may not share its enclosing type's name.
    /// </summary>
    [Precision(28, 10)]
    [Column("atr")]
    public decimal? AtrValue { get; set; }

    public KlineData KlineData { get; set; } = null!;
}
