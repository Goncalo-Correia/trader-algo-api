using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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
/// Background service that maintains a persistent Alpaca WebSocket connection for
/// all active Alpaca symbols. Publishes price ticks to PriceFeed on every trade
/// message and persists closed 1m / 1d bars to KlineData.
/// Higher-interval bars (5m, 15m, 1h, 4h) are handled by AlpacaRestPollingService.
/// </summary>
public sealed class AlpacaKlineStreamingService(
    IServiceScopeFactory                 scopeFactory,
    IConfiguration                       configuration,
    PriceFeed                            priceFeed,
    ClosedCandleFeed                     closedCandleFeed,
    CandleAggregator                     candleAggregator,
    NyseSessionService                   nyseSession,
    TimeProvider                         timeProvider,
    ILogger<AlpacaKlineStreamingService> logger) : BackgroundService
{
    private const string DefaultWsBaseUrl = "wss://stream.data.alpaca.markets";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<string> symbols;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            symbols = await db.Symbols
                .AsNoTracking()
                .Where(s => s.IsActive && s.ProviderId == (int)SymbolProvider.Alpaca)
                .Select(s => s.Code)
                .ToListAsync(stoppingToken);
        }

        if (symbols.Count == 0)
        {
            logger.LogInformation("No active Alpaca symbols — streaming service idle.");
            return;
        }

        logger.LogInformation("Alpaca stream will cover {Count} symbol(s): {Symbols}",
            symbols.Count, string.Join(", ", symbols));

        while (!stoppingToken.IsCancellationRequested)
        {
            // Outside market hours: sleep until the next open.
            var now = timeProvider.GetUtcNow();
            var nextOpen = nyseSession.NextMarketOpen(now);
            if (nextOpen > now.AddSeconds(30))
            {
                logger.LogInformation("Market closed. Alpaca stream reconnects at {NextOpen:u}", nextOpen);
                try { await Task.Delay(nextOpen - now, timeProvider, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }

            try
            {
                await RunStreamAsync(symbols, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Alpaca stream disconnected, reconnecting in 5 s");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    // ── Main stream loop ──────────────────────────────────────────────────────

    private async Task RunStreamAsync(List<string> symbols, CancellationToken ct)
    {
        var feed      = configuration["Alpaca:Feed"]           ?? "sip";
        var wsBaseUrl = configuration["Alpaca:WebSocketBaseUrl"] ?? DefaultWsBaseUrl;
        var apiKey    = configuration["Alpaca:ApiKey"]          ?? string.Empty;
        var secretKey = configuration["Alpaca:SecretKey"]       ?? string.Empty;

        var uri = new Uri($"{wsBaseUrl.TrimEnd('/')}/v2/{feed}");

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(uri, ct);

        await ReceiveFrameAsync(socket, ct); // "connected" message

        await SendJsonAsync(socket, new { action = "auth", key = apiKey, secret = secretKey }, ct);
        var authReply = await ReceiveFrameAsync(socket, ct);

        if (authReply is null || !authReply.Contains("\"authenticated\""))
            throw new InvalidOperationException($"Alpaca auth failed. Response: {authReply}");

        // Subscribe to 1m bars, daily bars, and trades for price ticks.
        await SendJsonAsync(socket,
            new { action = "subscribe", bars = symbols, dailyBars = symbols, trades = symbols }, ct);

        await ReceiveFrameAsync(socket, ct); // subscription confirmation

        logger.LogInformation("Alpaca WebSocket ready — subscribed to {Symbols}", string.Join(", ", symbols));

        await using var scope     = scopeFactory.CreateAsyncScope();
        var db                    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var indicatorSync         = scope.ServiceProvider.GetRequiredService<IIndicatorSyncService>();
        var symbolMap             = await BuildSymbolMapAsync(db, symbols, ct);
        var intervalMap           = await BuildIntervalMapAsync(db, ct);

        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var raw = await ReceiveRawAsync(socket, ct);
            if (raw is null) break;

            foreach (var msg in AlpacaStreamParser.Parse(raw))
            {
                switch (msg)
                {
                    case AlpacaTradeMessage trade:
                        priceFeed.Publish(trade.Symbol, trade.Price);
                        candleAggregator.OnTick(trade.Symbol, "1m", trade.Price, trade.Timestamp);
                        break;

                    case AlpacaMinuteBarMessage bar:
                        if (symbolMap.TryGetValue(bar.Symbol, out var sym1) &&
                            intervalMap.TryGetValue("1m", out var int1m))
                        {
                            priceFeed.Publish(bar.Symbol, bar.Close);
                            candleAggregator.OnCandleClosed(bar.Symbol, "1m",
                                bar.Timestamp, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume);
                            await PersistBarAsync(db, indicatorSync, bar.Symbol, "1m",
                                bar.Timestamp, bar.Open, bar.High, bar.Low, bar.Close,
                                bar.Volume, bar.NumberOfTrades, bar.Vwap,
                                sym1, int1m, ct);
                        }
                        break;

                    case AlpacaDailyBarMessage dBar:
                        if (symbolMap.TryGetValue(dBar.Symbol, out var symD) &&
                            intervalMap.TryGetValue("1d", out var int1d))
                        {
                            priceFeed.Publish(dBar.Symbol, dBar.Close);
                            candleAggregator.OnCandleClosed(dBar.Symbol, "1d",
                                dBar.Timestamp, dBar.Open, dBar.High, dBar.Low, dBar.Close, dBar.Volume);
                            await PersistBarAsync(db, indicatorSync, dBar.Symbol, "1d",
                                dBar.Timestamp, dBar.Open, dBar.High, dBar.Low, dBar.Close,
                                dBar.Volume, dBar.NumberOfTrades, dBar.Vwap,
                                symD, int1d, ct);
                        }
                        break;
                }
            }
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private async Task PersistBarAsync(
        ApplicationDbContext db,
        IIndicatorSyncService indicatorSync,
        string symbolCode,
        string intervalCode,
        DateTimeOffset openTime,
        decimal open, decimal high, decimal low, decimal close,
        long volume, int trades, decimal vwap,
        Symbol symbolEntity,
        Interval intervalEntity,
        CancellationToken ct)
    {
        var closeTime    = openTime.Add(intervalEntity.Duration).AddMilliseconds(-1);
        var dollarVolume = volume * vwap;

        var existing = await db.KlineData.SingleOrDefaultAsync(
            k => k.SymbolId   == symbolEntity.Id
              && k.IntervalId == intervalEntity.Id
              && k.OpenTime   == openTime, ct);

        if (existing is null)
        {
            db.KlineData.Add(new KlineData
            {
                SymbolId                = symbolEntity.Id,
                IntervalId              = intervalEntity.Id,
                OpenTime                = openTime,
                CloseTime               = closeTime,
                Open                    = open,
                High                    = high,
                Low                     = low,
                Close                   = close,
                Volume                  = volume,
                QuoteAssetVolume        = dollarVolume,
                NumberOfTrades          = trades,
                TakerBuyBaseAssetVolume  = 0m,
                TakerBuyQuoteAssetVolume = 0m,
                CreatedAt               = timeProvider.GetUtcNow(),
            });
        }
        else
        {
            existing.CloseTime        = closeTime;
            existing.Open             = open;
            existing.High             = high;
            existing.Low              = low;
            existing.Close            = close;
            existing.Volume           = volume;
            existing.QuoteAssetVolume = dollarVolume;
            existing.NumberOfTrades   = trades;
        }

        await db.SaveChangesAsync(ct);

        await indicatorSync.ComputeAndSaveAsync(
            symbolEntity.Id, intervalEntity.Id, openTime, openTime, ct);

        closedCandleFeed.Publish(new ClosedCandleEvent(symbolCode, intervalCode, openTime, close));

        logger.LogInformation("Stored Alpaca bar {Symbol}/{Interval} {OpenTime}",
            symbolCode, intervalCode, openTime);
    }

    // ── WebSocket helpers ─────────────────────────────────────────────────────

    private static async Task SendJsonAsync<T>(ClientWebSocket socket, T payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static async Task<string?> ReceiveFrameAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var raw = await ReceiveRawAsync(socket, ct);
        return raw is null ? null : Encoding.UTF8.GetString(raw);
    }

    private static async Task<byte[]?> ReceiveRawAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return ms.ToArray();
    }

    // ── DB helpers ────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, Symbol>> BuildSymbolMapAsync(
        ApplicationDbContext db, List<string> codes, CancellationToken ct) =>
        await db.Symbols.AsNoTracking()
            .Where(s => codes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code, ct);

    private static async Task<Dictionary<string, Interval>> BuildIntervalMapAsync(
        ApplicationDbContext db, CancellationToken ct) =>
        await db.Intervals.AsNoTracking()
            .ToDictionaryAsync(i => i.Code, ct);
}
