using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Binance;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Models;
using TraderAlgoApi.Services.Charts;

namespace TraderAlgoApi.Services.Binance;

public sealed class BinanceMarketDataWebSocketService(
    ApplicationDbContext dbContext,
    IConfiguration configuration,
    ILogger<BinanceMarketDataWebSocketService> logger,
    IChartsService chartsService) : IBinanceMarketDataWebSocketService
{
    private const string DefaultWebSocketBaseUrl = "wss://stream.binance.com:443";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task StreamKlinesAsync(
        WebSocket clientSocket,
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        await StreamKlinesAsync(
            clientSocket,
            symbol,
            interval,
            kline => (BinanceKlineStreamDto)kline,
            cancellationToken);
    }

    public async Task StreamKlineCandlesAsync(
        WebSocket clientSocket,
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        await StreamKlinesAsync(
            clientSocket,
            symbol,
            interval,
            kline => new CandleResponseDto(
                kline.Time,
                kline.Open,
                kline.High,
                kline.Low,
                kline.Close,
                kline.Volume),
            cancellationToken);
    }

    private async Task StreamKlinesAsync<TResponse>(
        WebSocket clientSocket,
        string symbol,
        string interval,
        Func<BinanceKlineStreamData, TResponse> responseFactory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        var streamUri = BuildKlineStreamUri(symbol, interval);

        using var binanceSocket = new ClientWebSocket();
        await binanceSocket.ConnectAsync(streamUri, cancellationToken);

        logger.LogInformation("Connected to Binance stream {StreamUri}", streamUri);

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   clientSocket.State == WebSocketState.Open &&
                   binanceSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextMessageAsync(binanceSocket, cancellationToken);
                if (message is null)
                {
                    break;
                }

                var streamData = ParseKlineMessage(message);
                if (streamData is null)
                {
                    continue;
                }

                if (streamData.IsClosed)
                {
                    try
                    {
                        await UpsertClosedKlineAsync(streamData, cancellationToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogError(
                            exception,
                            "Failed to store closed Binance kline {Symbol} {Interval} {OpenTime}.",
                            streamData.Symbol,
                            streamData.Interval,
                            streamData.OpenTime);
                    }
                }

                var payload = JsonSerializer.SerializeToUtf8Bytes(responseFactory(streamData), SerializerOptions);
                await clientSocket.SendAsync(
                    payload,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Binance kline stream was cancelled.");
        }
        catch (WebSocketException exception)
        {
            logger.LogWarning(exception, "Binance kline WebSocket stream ended unexpectedly.");
        }
        finally
        {
            if (clientSocket.State == WebSocketState.Open)
            {
                await clientSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Binance stream closed.",
                    CancellationToken.None);
            }
        }
    }

    private Uri BuildKlineStreamUri(string symbol, string interval)
    {
        var baseUrl = configuration["Binance:WebSocketBaseUrl"] ?? DefaultWebSocketBaseUrl;
        var streamName = $"{NormalizeSymbol(symbol)}@kline_{chartsService.NormalizeInterval(interval)}";

        return new Uri($"{baseUrl.TrimEnd('/')}/ws/{streamName}");
    }

    private static string NormalizeSymbol(string symbol)
    {
        var normalizedSymbol = symbol
            .Trim()
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalizedSymbol is "BTCUSD"
            ? "btcusdt"
            : normalizedSymbol.ToLowerInvariant();
    }



    private static async Task<byte[]?> ReceiveTextMessageAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return message.ToArray();
    }

    private async Task UpsertClosedKlineAsync(
        BinanceKlineStreamData kline,
        CancellationToken cancellationToken)
    {
        var symbolCode = ToDomainSymbolCode(kline.Symbol);
        var intervalCode = chartsService.NormalizeInterval(kline.Interval);

        var symbol = await dbContext.Symbols
            .SingleOrDefaultAsync(symbol => symbol.Code == symbolCode, cancellationToken);
        var interval = await GetOrCreateSupportedIntervalAsync(intervalCode, cancellationToken);

        if (symbol is null || interval is null)
        {
            logger.LogWarning(
                "Skipping closed Binance kline because symbol {SymbolCode} or interval {IntervalCode} does not exist.",
                symbolCode,
                intervalCode);
            return;
        }

        var existingKline = await dbContext.KlineData.SingleOrDefaultAsync(
            storedKline =>
                storedKline.SymbolId == symbol.Id &&
                storedKline.IntervalId == interval.Id &&
                storedKline.OpenTime == kline.OpenTime,
            cancellationToken);

        if (existingKline is null)
        {
            dbContext.KlineData.Add(new KlineData
            {
                SymbolId = symbol.Id,
                IntervalId = interval.Id,
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
            existingKline.CloseTime = kline.CloseTime;
            existingKline.Open = kline.Open;
            existingKline.High = kline.High;
            existingKline.Low = kline.Low;
            existingKline.Close = kline.Close;
            existingKline.Volume = kline.Volume;
            existingKline.QuoteAssetVolume = kline.QuoteAssetVolume;
            existingKline.NumberOfTrades = kline.NumberOfTrades;
            existingKline.TakerBuyBaseAssetVolume = kline.TakerBuyBaseAssetVolume;
            existingKline.TakerBuyQuoteAssetVolume = kline.TakerBuyQuoteAssetVolume;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Stored closed Binance kline {SymbolCode} {IntervalCode} {OpenTime}.",
            symbolCode,
            intervalCode,
            kline.OpenTime);
    }

    private async Task<Interval?> GetOrCreateSupportedIntervalAsync(
        string intervalCode,
        CancellationToken cancellationToken)
    {
        var interval = await dbContext.Intervals
            .SingleOrDefaultAsync(interval => interval.Code == intervalCode, cancellationToken);

        if (interval is not null)
        {
            return interval;
        }

        interval = intervalCode switch
        {
            "5m" => new Interval
            {
                Code = "5m",
                DisplayName = "5 Minute",
                Duration = TimeSpan.FromMinutes(5),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            "1h" => new Interval
            {
                Code = "1h",
                DisplayName = "1H",
                Duration = TimeSpan.FromHours(1),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            _ => null
        };

        if (interval is null)
        {
            return null;
        }

        dbContext.Intervals.Add(interval);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return interval;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();

            return await dbContext.Intervals
                .SingleOrDefaultAsync(storedInterval => storedInterval.Code == intervalCode, cancellationToken);
        }
    }

    private static BinanceKlineStreamData? ParseKlineMessage(byte[] message)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (!root.TryGetProperty("k", out var kline))
        {
            return null;
        }

        var eventTime = root.GetProperty("E").GetInt64();
        var openTime = kline.GetProperty("t").GetInt64();
        var closeTime = kline.GetProperty("T").GetInt64();

        return new BinanceKlineStreamData(
            Type: "kline",
            Symbol: kline.GetProperty("s").GetString() ?? string.Empty,
            Interval: kline.GetProperty("i").GetString() ?? string.Empty,
            EventTime: eventTime,
            Time: DateTimeOffset.FromUnixTimeMilliseconds(openTime).ToUnixTimeSeconds(),
            OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(openTime),
            CloseTime: DateTimeOffset.FromUnixTimeMilliseconds(closeTime),
            Open: GetDecimal(kline, "o"),
            High: GetDecimal(kline, "h"),
            Low: GetDecimal(kline, "l"),
            Close: GetDecimal(kline, "c"),
            Volume: GetDecimal(kline, "v"),
            QuoteAssetVolume: GetDecimal(kline, "q"),
            NumberOfTrades: kline.GetProperty("n").GetInt32(),
            TakerBuyBaseAssetVolume: GetDecimal(kline, "V"),
            TakerBuyQuoteAssetVolume: GetDecimal(kline, "Q"),
            IsClosed: kline.GetProperty("x").GetBoolean());
    }

    private static string ToDomainSymbolCode(string symbol)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        return normalizedSymbol is "BTCUSDT"
            ? "BTCUSD"
            : normalizedSymbol;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        return decimal.Parse(
            element.GetProperty(propertyName).GetString() ?? "0",
            NumberStyles.Number,
            CultureInfo.InvariantCulture);
    }

    private sealed record BinanceKlineStreamData(
        string Type,
        string Symbol,
        string Interval,
        long EventTime,
        long Time,
        DateTimeOffset OpenTime,
        DateTimeOffset CloseTime,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        decimal Volume,
        decimal QuoteAssetVolume,
        int NumberOfTrades,
        decimal TakerBuyBaseAssetVolume,
        decimal TakerBuyQuoteAssetVolume,
        bool IsClosed)
    {
        public static implicit operator BinanceKlineStreamDto(BinanceKlineStreamData data)
        {
            return new BinanceKlineStreamDto(
                data.Type,
                data.Symbol,
                data.Interval,
                data.EventTime,
                data.Time,
                data.Open,
                data.High,
                data.Low,
                data.Close,
                data.Volume,
                data.IsClosed);
        }
    }
}
