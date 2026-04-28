using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Symbol> Symbols => Set<Symbol>();

    public DbSet<Interval> Intervals => Set<Interval>();

    public DbSet<KlineData> KlineData => Set<KlineData>();

    public DbSet<Trade> Trades => Set<Trade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.HasIndex(symbol => symbol.Code).IsUnique();

            entity.HasData(new Symbol
            {
                Id = 1,
                Code = "BTCUSDT",
                BaseAsset = "BTC",
                QuoteAsset = "USDT",
                DisplayName = "BTC/USDT",
                IsActive = true,
                IsDefault = true,
                CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)
            });
        });

        modelBuilder.Entity<Interval>(entity =>
        {
            entity.HasIndex(interval => interval.Code).IsUnique();

            entity.HasData(
                new Interval { Id = 1, Code = "1m",  DisplayName = "1 Minute",  Duration = TimeSpan.FromMinutes(1),  IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 2, Code = "5m",  DisplayName = "5 Minute",  Duration = TimeSpan.FromMinutes(5),  IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 3, Code = "15m", DisplayName = "15 Minute", Duration = TimeSpan.FromMinutes(15), IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 4, Code = "1h",  DisplayName = "1H",        Duration = TimeSpan.FromHours(1),    IsActive = true, IsDefault = true,  CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 5, Code = "4h",  DisplayName = "4H",        Duration = TimeSpan.FromHours(4),    IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) },
                new Interval { Id = 6, Code = "1d",  DisplayName = "1D",        Duration = TimeSpan.FromDays(1),     IsActive = true, IsDefault = false, CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero) });
        });

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.Property(t => t.Side).HasConversion<string>();
            entity.Property(t => t.OrderType).HasConversion<string>();
            entity.Property(t => t.Status).HasConversion<string>();
            entity.Property(t => t.CloseReason).HasConversion<string>();

            entity.HasIndex(t => new { t.SymbolCode, t.Status });
        });

        modelBuilder.Entity<KlineData>(entity =>
        {
            entity.HasIndex(kline => new { kline.SymbolId, kline.IntervalId, kline.OpenTime }).IsUnique();

            entity.HasOne(kline => kline.Symbol)
                .WithMany(symbol => symbol.Klines)
                .HasForeignKey(kline => kline.SymbolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(kline => kline.Interval)
                .WithMany(interval => interval.Klines)
                .HasForeignKey(kline => kline.IntervalId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
