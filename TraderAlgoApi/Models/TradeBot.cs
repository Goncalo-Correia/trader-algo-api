using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Lookups;

namespace TraderAlgoApi.Models;

[Table("trade_bots")]
public sealed class TradeBot
{
    public long Id { get; set; }

    public long? TradingAccountId { get; set; }

    [ForeignKey(nameof(TradingAccountId))]
    public TradingAccount? TradingAccount { get; set; }

    public long? BacktestId { get; set; }

    [ForeignKey(nameof(BacktestId))]
    public Backtest? Backtest { get; set; }

    public int TradingStrategyId { get; set; }

    [ForeignKey(nameof(TradingStrategyId))]
    public TradingStrategy TradingStrategy { get; set; } = null!;

    public long? MlPolicyId { get; set; }

    [ForeignKey(nameof(MlPolicyId))]
    public MlPolicy? MlPolicy { get; set; }

    /// <summary>
    /// Strongly-typed view over <see cref="TradingStrategyId"/> (lookup IDs match the enum values).
    /// The single home for the int↔enum mapping; not mapped and not usable in EF LINQ queries.
    /// </summary>
    [NotMapped]
    public Enums.TradingStrategy StrategyEnum
    {
        get => (Enums.TradingStrategy)TradingStrategyId;
        set => TradingStrategyId = (int)value;
    }

    public int SymbolId { get; set; }

    [ForeignKey(nameof(SymbolId))]
    public Symbol Symbol { get; set; } = null!;

    public int IntervalId { get; set; }

    [ForeignKey(nameof(IntervalId))]
    public Interval Interval { get; set; } = null!;

    public bool IsEnabled { get; set; }

    [Precision(28, 10)]
    public decimal Quantity { get; set; }

    [Precision(28, 10)]
    public decimal? StopLoss { get; set; }

    [Precision(28, 10)]
    public decimal? TakeProfit { get; set; }

    [Precision(28, 10)]
    public decimal? Breakeven { get; set; }

    [Precision(28, 10)]
    public decimal? BreakevenStop { get; set; }

    public bool IsNySessionOnly { get; set; }

    public bool Delay { get; set; }

    [Precision(28, 10)]
    public decimal? DailyProfitGoal { get; set; }

    public int? MaxLossesPerDay { get; set; }

    public int? MaxCandlesPerTrade { get; set; }

    [Precision(28, 10)]
    public decimal Fee { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastSignalAt { get; set; }
}
