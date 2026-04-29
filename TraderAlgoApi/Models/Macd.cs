using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TraderAlgoApi.Models;

[Table("macd")]
public sealed class Macd
{
    // KlineDataId is both the primary key and the foreign key — one-to-one with KlineData.
    public long KlineDataId { get; set; }

    /// <summary>Fast EMA (12) minus slow EMA (26). Null for the first 25 candles.</summary>
    [Precision(28, 10)]
    [Column("macd_line")]
    public decimal? MacdLine { get; set; }

    /// <summary>9-period EMA of the MACD line. Null until 34 candles of history are available.</summary>
    [Precision(28, 10)]
    [Column("signal_line")]
    public decimal? SignalLine { get; set; }

    /// <summary>MACD line minus signal line. Null when either component is null.</summary>
    [Precision(28, 10)]
    [Column("histogram")]
    public decimal? Histogram { get; set; }

    public KlineData KlineData { get; set; } = null!;
}
