using System.Net.WebSockets;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.PriceFeeds;

namespace TraderAlgoApi.Services.Binance;

public sealed class BinanceKlineStreamingService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    PriceFeed priceFeed,
    ILogger<BinanceKlineStreamingService> logger) : BackgroundService
{
    private const string DefaultWebSocketBaseUrl = "wss://stream.binance.com:443";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<(string Symbol, string Interval)> combos;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            combos = await LoadActiveCombosAsync(dbContext, stoppingToken);
        }

        logger.LogInformation("Starting kline streams for {Count} symbol/interval combos", combos.Count);

        var tasks = combos.Select(c => RunStreamAsync(c.Symbol, c.Interval, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task RunStreamAsync(string symbol, string interval, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StreamAsync(symbol, interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kline stream {Symbol}/{Interval} disconnected, reconnecting in 5s", symbol, interval);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task StreamAsync(string symbol, string interval, CancellationToken stoppingToken)
    {
        var uri = BuildStreamUri(symbol, interval);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(uri, stoppingToken);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var indicatorSyncService = scope.ServiceProvider.GetRequiredService<IIndicatorSyncService>();

        var symbolEntity = await dbContext.Symbols
            .SingleOrDefaultAsync(s => s.Code == symbol, stoppingToken);
        var intervalEntity = await dbContext.Intervals
            .SingleOrDefaultAsync(i => i.Code == interval, stoppingToken);

        if (symbolEntity is null || intervalEntity is null)
        {
            logger.LogWarning("Symbol {Symbol} or interval {Interval} not found in database, skipping stream", symbol, interval);
            return;
        }

        logger.LogInformation("Kline stream connected: {Symbol}/{Interval}", symbol, interval);

        while (!stoppingToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var message = await ReceiveMessageAsync(socket, stoppingToken);
            if (message is null)
                break;

            var kline = BinanceKlineStream.FromJson(message);
            if (kline is null)
                continue;

            priceFeed.Publish(kline.Symbol, kline.Close);

            if (!kline.IsClosed)
                continue;

            await PersistKlineAsync(dbContext, indicatorSyncService, kline, symbolEntity.Id, intervalEntity.Id, stoppingToken);
        }
    }

    private async Task PersistKlineAsync(
        ApplicationDbContext dbContext,
        IIndicatorSyncService indicatorSyncService,
        BinanceKlineStream kline,
        int symbolId,
        int intervalId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.KlineData.SingleOrDefaultAsync(
            k => k.SymbolId == symbolId && k.IntervalId == intervalId && k.OpenTime == kline.OpenTime,
            cancellationToken);

        if (existing is null)
        {
            dbContext.KlineData.Add(new KlineData
            {
                SymbolId = symbolId,
                IntervalId = intervalId,
                OpenTime = kline.OpenTime,
                CloseTime = kline.CloseTime,
                Open = kline.Open,
                High = kline.High,
                Low = kline.Low,
                Close = kline.Close,
                Volume = kline.Volume,
                QuoteAssetVolume = kline.QuoteAssetVolume,
                NumberOfTrades = kline.NumberOfTrades,
                TakerBuyBaseAssetVolume = kline.TakerBuyBaseAssetVolume,
                TakerBuyQuoteAssetVolume = kline.TakerBuyQuoteAssetVolume,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.CloseTime = kline.CloseTime;
            existing.Open = kline.Open;
            existing.High = kline.High;
            existing.Low = kline.Low;
            existing.Close = kline.Close;
            existing.Volume = kline.Volume;
            existing.QuoteAssetVolume = kline.QuoteAssetVolume;
            existing.NumberOfTrades = kline.NumberOfTrades;
            existing.TakerBuyBaseAssetVolume = kline.TakerBuyBaseAssetVolume;
            existing.TakerBuyQuoteAssetVolume = kline.TakerBuyQuoteAssetVolume;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await indicatorSyncService.ComputeAndSaveAsync(
            symbolId, intervalId, kline.OpenTime, kline.OpenTime, cancellationToken);

        logger.LogInformation(
            "Stored closed kline {Symbol}/{Interval} {OpenTime}",
            kline.Symbol, kline.Interval, kline.OpenTime);
    }

    private static async Task<List<(string Symbol, string Interval)>> LoadActiveCombosAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var symbols = await dbContext.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => s.Code)
            .ToListAsync(cancellationToken);

        var intervals = await dbContext.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive)
            .Select(i => i.Code)
            .ToListAsync(cancellationToken);

        return symbols
            .SelectMany(s => intervals.Select(i => (s, i)))
            .ToList();
    }

    private Uri BuildStreamUri(string symbol, string interval)
    {
        var baseUrl = configuration["Binance:WebSocketBaseUrl"] ?? DefaultWebSocketBaseUrl;
        var streamName = $"{symbol.ToLowerInvariant()}@kline_{interval}";
        return new Uri($"{baseUrl.TrimEnd('/')}/ws/{streamName}");
    }

    private static async Task<byte[]?> ReceiveMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return stream.ToArray();
    }
}
