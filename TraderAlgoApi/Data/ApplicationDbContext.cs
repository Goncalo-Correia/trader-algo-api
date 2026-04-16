using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models;

namespace TraderAlgoApi.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Symbol> Symbols => Set<Symbol>();

    public DbSet<Interval> Intervals => Set<Interval>();

    public DbSet<KlineData> KlineData => Set<KlineData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.HasIndex(symbol => symbol.Code).IsUnique();

            entity.HasData(new Symbol
            {
                Id = 1,
                Code = "BTCUSD",
                BaseAsset = "BTC",
                QuoteAsset = "USD",
                DisplayName = "BTC/USD",
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)
            });
        });

        modelBuilder.Entity<Interval>(entity =>
        {
            entity.HasIndex(interval => interval.Code).IsUnique();

            entity.HasData(
                new Interval
                {
                    Id = 1,
                    Code = "1h",
                    DisplayName = "1H",
                    Duration = TimeSpan.FromHours(1),
                    IsActive = true,
                    CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)
                },
                new Interval
                {
                    Id = 2,
                    Code = "5m",
                    DisplayName = "5 Minute",
                    Duration = TimeSpan.FromMinutes(5),
                    IsActive = true,
                    CreatedAt = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)
                });
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
