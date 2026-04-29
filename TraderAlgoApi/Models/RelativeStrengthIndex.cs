using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

[Table("relative_strength_indexes")]
public sealed class RelativeStrengthIndex
{
    // KlineDataId is both the primary key and the foreign key — one-to-one with KlineData.
    public long KlineDataId { get; set; }

    /// <summary>RSI value (0–100). Null for the first 14 candles (insufficient history).</summary>
    [Precision(28, 10)]
    [Column("rsi")]
    public decimal? Rsi { get; set; }

    /// <summary>3-period SMA of RSI. Null until three consecutive RSI values are available.</summary>
    [Precision(28, 10)]
    [Column("rsi_smooth")]
    public decimal? RsiSmooth { get; set; }

    /// <summary>
    /// True when bullish or bearish divergence is detected against the prior 5-candle window.
    /// Bullish: price lower low + RSI higher low.
    /// Bearish: price higher high + RSI lower high.
    /// Null when there is insufficient history to evaluate.
    /// </summary>
    [Column("divergence")]
    public bool? Divergence { get; set; }

    public KlineData KlineData { get; set; } = null!;
}
