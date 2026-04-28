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

    public DbSet<TradeSide>       TradeSides       => Set<TradeSide>();
    public DbSet<TradeOrderType>  TradeOrderTypes  => Set<TradeOrderType>();
    public DbSet<TradeStatus>     TradeStatuses    => Set<TradeStatus>();
    public DbSet<TradeCloseReason> TradeCloseReasons => Set<TradeCloseReason>();

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
            new TradeCloseReason { Id = 3, Name = "TakeProfit" });

        // -------------------------------------------------------------------
        // Trades — FK relationships to lookup tables, composite index
        // -------------------------------------------------------------------
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasOne<TradeSide>()
                .WithMany()
                .HasForeignKey(t => t.SideId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TradeOrderType>()
                .WithMany()
                .HasForeignKey(t => t.OrderTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TradeStatus>()
                .WithMany()
                .HasForeignKey(t => t.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TradeCloseReason>()
                .WithMany()
                .HasForeignKey(t => t.CloseReasonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => new { t.SymbolCode, t.StatusId });
        });
    }
}
