using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Lookups;

namespace TraderAlgoApi.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Symbol>    Symbols   => Set<Symbol>();
    public DbSet<Interval>  Intervals => Set<Interval>();
    public DbSet<KlineData> KlineData => Set<KlineData>();
    public DbSet<Trade>     Trades    => Set<Trade>();
    public DbSet<TradeBot>  TradeBots => Set<TradeBot>();
    public DbSet<Backtest>  Backtests => Set<Backtest>();

    public DbSet<TradeSide>       TradeSides       => Set<TradeSide>();
    public DbSet<TradeOrderType>  TradeOrderTypes  => Set<TradeOrderType>();
    public DbSet<TradeStatus>     TradeStatuses    => Set<TradeStatus>();
    public DbSet<TradeCloseReason> TradeCloseReasons => Set<TradeCloseReason>();

    public DbSet<SimpleMovingAverage> SimpleMovingAverages => Set<SimpleMovingAverage>();

    public DbSet<RelativeStrengthIndex> RelativeStrengthIndexes => Set<RelativeStrengthIndex>();

    public DbSet<Macd> Macd => Set<Macd>();

    public DbSet<TradingStrategy>  TradingStrategies  => Set<TradingStrategy>();
    public DbSet<TradingAccount>   TradingAccounts    => Set<TradingAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // -------------------------------------------------------------------
        // Symbols
        // -------------------------------------------------------------------
        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.HasIndex(s => s.Code).IsUnique();

            entity.HasData(new Symbol
            {
                Id          = 1,
                Code        = "BTCUSDT",
                BaseAsset   = "BTC",
                QuoteAsset  = "USDT",
                DisplayName = "BTC/USDT",
                IsActive    = true,
                IsDefault   = true,
                CreatedAt   = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)
            });
        });

        // -------------------------------------------------------------------
        // Intervals
        // -------------------------------------------------------------------
        modelBuilder.Entity<Interval>(entity =>
        {
            entity.HasIndex(i => i.Code).IsUnique();

            entity.HasData(
                new Interval { Id = 1, Code = "1m",  DisplayName = "1 Minute",  Duration = TimeSpan.FromMinutes(1),  IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 2, Code = "5m",  DisplayName = "5 Minute",  Duration = TimeSpan.FromMinutes(5),  IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 3, Code = "15m", DisplayName = "15 Minute", Duration = TimeSpan.FromMinutes(15), IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 4, Code = "1h",  DisplayName = "1H",        Duration = TimeSpan.FromHours(1),    IsActive = true, IsDefault = true,  CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 5, Code = "4h",  DisplayName = "4H",        Duration = TimeSpan.FromHours(4),    IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 6, Code = "1d",  DisplayName = "1D",        Duration = TimeSpan.FromDays(1),     IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) });
        });

        // -------------------------------------------------------------------
        // KlineData
        // -------------------------------------------------------------------
        modelBuilder.Entity<KlineData>(entity =>
        {
            entity.HasIndex(k => new { k.SymbolId, k.IntervalId, k.OpenTime }).IsUnique();

            entity.HasOne(k => k.Symbol)
                .WithMany(s => s.Klines)
                .HasForeignKey(k => k.SymbolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(k => k.Interval)
                .WithMany(i => i.Klines)
                .HasForeignKey(k => k.IntervalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -------------------------------------------------------------------
        // Trade lookup tables — IDs match the C# enum values
        // -------------------------------------------------------------------
        modelBuilder.Entity<TradeSide>().HasData(
            new TradeSide { Id = 1, Name = "Buy"  },
            new TradeSide { Id = 2, Name = "Sell" });

        modelBuilder.Entity<TradeOrderType>().HasData(
            new TradeOrderType { Id = 1, Name = "Market" },
            new TradeOrderType { Id = 2, Name = "Limit"  });

        modelBuilder.Entity<TradeStatus>().HasData(
            new TradeStatus { Id = 1, Name = "Pending"   },
            new TradeStatus { Id = 2, Name = "Active"    },
            new TradeStatus { Id = 3, Name = "Closed"    },
            new TradeStatus { Id = 4, Name = "Cancelled" });

        modelBuilder.Entity<TradeCloseReason>().HasData(
            new TradeCloseReason { Id = 1, Name = "Manual"     },
            new TradeCloseReason { Id = 2, Name = "StopLoss"   },
            new TradeCloseReason { Id = 3, Name = "TakeProfit" },
            new TradeCloseReason { Id = 4, Name = "BotSignal"  });

        // -------------------------------------------------------------------
        // Trades — FK relationships to lookup tables, composite index
        // -------------------------------------------------------------------
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasOne(t => t.Symbol)
                .WithMany()
                .HasForeignKey(t => t.SymbolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Interval)
                .WithMany()
                .HasForeignKey(t => t.IntervalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Side)
                .WithMany()
                .HasForeignKey(t => t.SideId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.OrderType)
                .WithMany()
                .HasForeignKey(t => t.OrderTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Status)
                .WithMany()
                .HasForeignKey(t => t.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.CloseReason)
                .WithMany()
                .HasForeignKey(t => t.CloseReasonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => new { t.SymbolId, t.StatusId });
        });

        // -------------------------------------------------------------------
        // TradingStrategy — IDs match the C# enum values
        // -------------------------------------------------------------------
        modelBuilder.Entity<TradingStrategy>().HasData(
            new TradingStrategy { Id = 1, Name = "SMA"  },
            new TradingStrategy { Id = 2, Name = "RSI"  },
            new TradingStrategy { Id = 3, Name = "MACD" });

        // -------------------------------------------------------------------
        // TradingAccounts
        // -------------------------------------------------------------------
        modelBuilder.Entity<TradingAccount>(entity =>
        {
            entity.HasOne(a => a.TradingStrategy)
                .WithMany()
                .HasForeignKey(a => a.TradingStrategyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(new TradingAccount
            {
                Id                = 1,
                Name              = "Default",
                InitialBalance    = 1000m,
                CurrentBalance    = 1000m,
                TradingStrategyId = 1,
                IsActive          = true,
                CreatedAt         = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)
            });
        });

        // -------------------------------------------------------------------
        // Trade — TradingAccount FK (nullable, restrict)
        // -------------------------------------------------------------------
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasOne(t => t.TradingAccount)
                .WithMany(a => a.Trades)
                .HasForeignKey(t => t.TradingAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -------------------------------------------------------------------
        // TradeBots
        // -------------------------------------------------------------------
        modelBuilder.Entity<TradeBot>(entity =>
        {
            entity.HasIndex(b => b.TradingAccountId).IsUnique();

            entity.HasOne(b => b.TradingAccount)
                .WithOne(a => a.TradeBot)
                .HasForeignKey<TradeBot>(b => b.TradingAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Symbol)
                .WithMany()
                .HasForeignKey(b => b.SymbolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Interval)
                .WithMany()
                .HasForeignKey(b => b.IntervalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -------------------------------------------------------------------
        // Backtests
        // -------------------------------------------------------------------
        modelBuilder.Entity<Backtest>(entity =>
        {
            entity.HasOne(b => b.Symbol)
                .WithMany()
                .HasForeignKey(b => b.SymbolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Interval)
                .WithMany()
                .HasForeignKey(b => b.IntervalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.TradingStrategy)
                .WithMany()
                .HasForeignKey(b => b.TradingStrategyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.TradeBot)
                .WithMany()
                .HasForeignKey(b => b.TradeBotId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // -------------------------------------------------------------------
        // Trade — Backtest FK (nullable, restrict; cascade delete from Backtest)
        // -------------------------------------------------------------------
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasOne(t => t.Backtest)
                .WithMany(b => b.Trades)
                .HasForeignKey(t => t.BacktestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SimpleMovingAverage>(entity =>
        {
            entity.HasKey(sma => sma.KlineDataId);

            entity.HasOne(sma => sma.KlineData)
                .WithOne(kline => kline.SimpleMovingAverage)
                .HasForeignKey<SimpleMovingAverage>(sma => sma.KlineDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RelativeStrengthIndex>(entity =>
        {
            entity.HasKey(rsi => rsi.KlineDataId);

            entity.HasOne(rsi => rsi.KlineData)
                .WithOne(kline => kline.RelativeStrengthIndex)
                .HasForeignKey<RelativeStrengthIndex>(rsi => rsi.KlineDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Macd>(entity =>
        {
            entity.HasKey(macd => macd.KlineDataId);

            entity.HasOne(macd => macd.KlineData)
                .WithOne(kline => kline.Macd)
                .HasForeignKey<Macd>(macd => macd.KlineDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
