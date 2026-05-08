using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;
using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.MarketData;
using TraderAlgoApi.Services.PriceFeeds;
using TraderAlgoApi.Services.Session;

namespace TraderAlgoApi.Services.MarketData.Alpaca;

/// <summary>
/// Polls the Alpaca REST API once per minute and persists any newly closed bars
/// for the 5m, 15m, 1h, and 4h intervals (which are not delivered over WebSocket).
/// Runs only during NYSE market hours. A 35-second delay after each interval
/// boundary gives Alpaca time to finalise and serve the completed bar.
/// </summary>
public sealed class AlpacaRestPollingService(
    IServiceScopeFactory              scopeFactory,
    NyseSessionService                nyseSession,
    ILogger<AlpacaRestPollingService> logger) : BackgroundService
{
    // Intervals not streamed via WebSocket — must be polled.
    private static readonly string[] PolledIntervalCodes = ["5m", "15m", "1h", "4h"];

    // Delay after interval boundary before fetching (lets Alpaca finalise the bar).
    private static readonly TimeSpan BarFinaliseDelay = TimeSpan.FromSeconds(35);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!nyseSession.IsMarketOpen(DateTimeOffset.UtcNow))
                continue;

            try
            {
                await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Alpaca REST poll failed");
            }
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        await using var scope     = scopeFactory.CreateAsyncScope();
        var db                    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var indicatorSync         = scope.ServiceProvider.GetRequiredService<IIndicatorSyncService>();
        var alpacaProvider        = scope.ServiceProvider.GetRequiredService<AlpacaMarketDataProvider>();
        var closedCandleFeed      = scope.ServiceProvider.GetRequiredService<ClosedCandleFeed>();
        var priceFeed             = scope.ServiceProvider.GetRequiredService<PriceFeed>();

        var candleAggregator = scope.ServiceProvider.GetRequiredService<CandleAggregator>();

        var alpacaSymbols = await db.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive && s.Provider == SymbolProvider.Alpaca)
            .ToListAsync(ct);

        var intervals = await db.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive && PolledIntervalCodes.Contains(i.Code))
            .ToListAsync(ct);

        foreach (var symbol in alpacaSymbols)
        {
            foreach (var interval in intervals)
            {
                var lastBarOpenTime = LastCompletedBarOpenTime(now, interval.Duration);

                // Check if this bar is already stored.
                var alreadyExists = await db.KlineData.AnyAsync(
                    k => k.SymbolId   == symbol.Id
                      && k.IntervalId == interval.Id
                      && k.OpenTime   == lastBarOpenTime, ct);

                if (alreadyExists)
                    continue;

                // Fetch the single completed bar from REST.
                var candles = await alpacaProvider.GetCandlesAsync(
                    symbol.Code,
                    interval.Code,
                    startTime: lastBarOpenTime,
                    endTime:   lastBarOpenTime.Add(interval.Duration),
                    limit:     1,
                    ct);

                var candle = candles.FirstOrDefault(c => c.OpenTime == lastBarOpenTime);
                if (candle is null)
                {
                    logger.LogDebug("No Alpaca bar yet for {Symbol}/{Interval} {OpenTime}",
                        symbol.Code, interval.Code, lastBarOpenTime);
                    continue;
                }

                db.KlineData.Add(new KlineData
                {
                    SymbolId                = symbol.Id,
                    IntervalId              = interval.Id,
                    OpenTime                = candle.OpenTime,
                    CloseTime               = candle.CloseTime,
                    Open                    = candle.Open,
                    High                    = candle.High,
                    Low                     = candle.Low,
                    Close                   = candle.Close,
                    Volume                  = candle.Volume,
                    QuoteAssetVolume        = candle.QuoteAssetVolume,
                    NumberOfTrades          = candle.NumberOfTrades,
                    TakerBuyBaseAssetVolume  = 0m,
                    TakerBuyQuoteAssetVolume = 0m,
                    CreatedAt               = DateTimeOffset.UtcNow,
                });

                await db.SaveChangesAsync(ct);

                await indicatorSync.ComputeAndSaveAsync(
                    symbol.Id, interval.Id, candle.OpenTime, candle.OpenTime, ct);

                priceFeed.Publish(symbol.Code, candle.Close);

                candleAggregator.OnCandleClosed(
                    symbol.Code, interval.Code,
                    candle.OpenTime, candle.Open, candle.High, candle.Low, candle.Close, candle.Volume);

                closedCandleFeed.Publish(new ClosedCandleEvent(
                    symbol.Code, interval.Code, candle.OpenTime, candle.Close));

                logger.LogInformation("REST-polled Alpaca bar {Symbol}/{Interval} {OpenTime}",
                    symbol.Code, interval.Code, candle.OpenTime);
            }
        }
    }

    /// <summary>
    /// Returns the OpenTime of the most recently completed bar for the given duration
    /// at the given wall-clock time, accounting for the finalise delay.
    /// </summary>
    private static DateTimeOffset LastCompletedBarOpenTime(DateTimeOffset now, TimeSpan duration)
    {
        var effectiveNow = now - BarFinaliseDelay;
        var ticks        = effectiveNow.UtcTicks / duration.Ticks;
        return new DateTimeOffset(ticks * duration.Ticks - duration.Ticks, TimeSpan.Zero);
    }
}
