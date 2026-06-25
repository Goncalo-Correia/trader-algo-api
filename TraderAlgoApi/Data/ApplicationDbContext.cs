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

    public DbSet<BacktestStatus> BacktestStatuses => Set<BacktestStatus>();
    public DbSet<SymbolProvider> SymbolProviders  => Set<SymbolProvider>();

    public DbSet<MlPolicy> MlPolicies => Set<MlPolicy>();
    public DbSet<MlTrainingRun> MlTrainingRuns => Set<MlTrainingRun>();
    public DbSet<MlTrainingRunStatus> MlTrainingRunStatuses => Set<MlTrainingRunStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // -------------------------------------------------------------------
        // Symbols
        // -------------------------------------------------------------------
        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.HasIndex(s => s.Code).IsUnique();

            entity.HasOne(s => s.Provider)
                .WithMany()
                .HasForeignKey(s => s.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                new Symbol
                {
                    Id          = 1,
                    Code        = "BTCUSDT",
                    BaseAsset   = "BTC",
                    QuoteAsset  = "USDT",
                    DisplayName = "BTC/USDT",
                    IsActive    = true,
                    IsDefault   = true,
                    ProviderId  = (int)Models.Enums.SymbolProvider.Binance,
                    CreatedAt   = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)
                },
                new Symbol
                {
                    Id          = 3,
                    Code        = "ETHUSDT",
                    BaseAsset   = "ETH",
                    QuoteAsset  = "USDT",
                    DisplayName = "ETH/USDT",
                    IsActive    = true,
                    IsDefault   = false,
                    ProviderId  = (int)Models.Enums.SymbolProvider.Binance,
                    CreatedAt   = new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero)
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
            new TradingStrategy { Id = 1, Name = "SMA"      },
            new TradingStrategy { Id = 2, Name = "RSI"      },
            new TradingStrategy { Id = 3, Name = "MACD"     },
            new TradingStrategy { Id = 4, Name = "SMA MACD" },
            new TradingStrategy { Id = 5, Name = "ML Policy" });

        // -------------------------------------------------------------------
        // SymbolProvider — IDs match the C# enum values
        // -------------------------------------------------------------------
        modelBuilder.Entity<SymbolProvider>().HasData(
            new SymbolProvider { Id = 1, Name = "Binance" });

        // -------------------------------------------------------------------
        // BacktestStatus — IDs match the C# enum values
        // -------------------------------------------------------------------
        modelBuilder.Entity<BacktestStatus>().HasData(
            new BacktestStatus { Id = 1, Name = "Pending"   },
            new BacktestStatus { Id = 2, Name = "Running"   },
            new BacktestStatus { Id = 3, Name = "Completed" },
            new BacktestStatus { Id = 4, Name = "Failed"    },
            new BacktestStatus { Id = 5, Name = "Cancelled" });

        // -------------------------------------------------------------------
        // MlTrainingRunStatus — IDs match the C# enum values
        // -------------------------------------------------------------------
        modelBuilder.Entity<MlTrainingRunStatus>().HasData(
            new MlTrainingRunStatus { Id = 1, Name = "Pending"   },
            new MlTrainingRunStatus { Id = 2, Name = "Running"   },
            new MlTrainingRunStatus { Id = 3, Name = "Completed" },
            new MlTrainingRunStatus { Id = 4, Name = "Failed"    });

        // -------------------------------------------------------------------
        // MlPolicies — training configuration (symbol/interval + hyperparameters)
        // -------------------------------------------------------------------
        modelBuilder.Entity<MlPolicy>(entity =>
        {
            entity.HasOne(p => p.Symbol)
                .WithMany()
                .HasForeignKey(p => p.SymbolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Interval)
                .WithMany()
                .HasForeignKey(p => p.IntervalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -------------------------------------------------------------------
        // MlTrainingRuns — each run executes a policy over a date range
        // -------------------------------------------------------------------
        modelBuilder.Entity<MlTrainingRun>(entity =>
        {
            entity.HasIndex(r => r.MlPolicyId);

            entity.HasOne(r => r.Policy)
                .WithMany(p => p.TrainingRuns)
                .HasForeignKey(r => r.MlPolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Status)
                .WithMany()
                .HasForeignKey(r => r.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -------------------------------------------------------------------
        // TradingAccounts
        // -------------------------------------------------------------------
        modelBuilder.Entity<TradingAccount>(entity =>
        {
            entity.HasData(new TradingAccount
            {
                Id             = 1,
                Name           = "Default",
                InitialBalance = 1000m,
                CurrentBalance = 1000m,
                IsActive       = true,
                CreatedAt      = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)
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
            entity.HasIndex(b => b.TradingAccountId);

            entity.HasIndex(b => b.BacktestId);

            entity.HasIndex(b => b.TradingStrategyId);

            entity.HasIndex(b => b.MlPolicyId);

            entity.HasIndex(b => b.TradingAccountId)
                .IsUnique()
                .HasFilter("\"TradingAccountId\" IS NOT NULL AND \"IsEnabled\"");

            entity.HasIndex(b => b.BacktestId)
                .IsUnique()
                .HasFilter("\"BacktestId\" IS NOT NULL AND \"IsEnabled\"");

            entity.HasOne(b => b.TradingAccount)
                .WithMany(a => a.TradeBots)
                .HasForeignKey(b => b.TradingAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Backtest)
                .WithMany(b => b.TradeBots)
                .HasForeignKey(b => b.BacktestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.TradingStrategy)
                .WithMany()
                .HasForeignKey(b => b.TradingStrategyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.MlPolicy)
                .WithMany(p => p.TradeBots)
                .HasForeignKey(b => b.MlPolicyId)
                .OnDelete(DeleteBehavior.Restrict);

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

            entity.HasOne(b => b.TradeBot)
                .WithMany()
                .HasForeignKey(b => b.TradeBotId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(b => b.Status)
                .WithMany()
                .HasForeignKey(b => b.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
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
