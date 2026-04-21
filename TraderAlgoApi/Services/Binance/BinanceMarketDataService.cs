using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Models;

namespace TraderAlgoApi.Services.Binance;

public sealed class BinanceMarketDataService(
    IHttpClientFactory httpClientFactory,
    ApplicationDbContext dbContext,
    IConfiguration configuration,
    ILogger<BinanceMarketDataService> logger) : IBinanceMarketDataService
{
    private const string HttpClientName = "Binance";
    private const string DefaultWebSocketBaseUrl = "wss://stream.binance.com:443";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<BinanceKline>> GetKlinesAsync(
        string symbol,
        string interval,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        var queryParameters = new List<string>
        {
            $"symbol={Uri.EscapeDataString(symbol)}",
            $"interval={Uri.EscapeDataString(interval)}"
        };

        if (startTime is not null)
            queryParameters.Add($"startTime={startTime.Value.ToUnixTimeMilliseconds()}");

        if (endTime is not null)
            queryParameters.Add($"endTime={endTime.Value.ToUnixTimeMilliseconds()}");

        if (limit is not null)
            queryParameters.Add($"limit={limit.Value}");

        using var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.GetAsync(
            $"/api/v3/klines?{string.Join('&', queryParameters)}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind is not JsonValueKind.Array)
            throw new JsonException("Unexpected Binance klines response.");

        return document.RootElement
            .EnumerateArray()
            .Select(BinanceKline.FromJsonArray)
            .ToArray();
    }

    public async Task StreamKlineCandlesAsync(
        WebSocket clientSocket,
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        await StreamKlinesInternalAsync(
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

    private async Task StreamKlinesInternalAsync<TResponse>(
        WebSocket clientSocket,
        string symbol,
        string interval,
        Func<BinanceKlineStreamData, TResponse> responseFactory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        var streamUri = BuildStreamUri(symbol, interval);

        using var binanceSocket = new ClientWebSocket();
        await binanceSocket.ConnectAsync(streamUri, cancellationToken);

        logger.LogInformation("Connected to Binance stream {StreamUri}", streamUri);

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   clientSocket.State == WebSocketState.Open &&
                   binanceSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveMessageAsync(binanceSocket, cancellationToken);
                if (message is null)
                    break;

                var streamData = ParseKlineMessage(message);
                if (streamData is null)
                    continue;

                if (streamData.IsClosed)
                {
                    try
                    {
                        await UpsertClosedKlineAsync(streamData, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(
                            ex,
                            "Failed to store closed Binance kline {Symbol} {Interval} {OpenTime}.",
                            streamData.Symbol,
                            streamData.Interval,
                            streamData.OpenTime);
                    }
                }

                var payload = JsonSerializer.SerializeToUtf8Bytes(responseFactory(streamData), SerializerOptions);
                await clientSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Binance kline stream was cancelled.");
        }
        catch (WebSocketException ex)
        {
            logger.LogWarning(ex, "Binance kline WebSocket stream ended unexpectedly.");
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

    private async Task UpsertClosedKlineAsync(BinanceKlineStreamData kline, CancellationToken cancellationToken)
    {
        var symbol = await dbContext.Symbols
            .SingleOrDefaultAsync(s => s.Code == kline.Symbol, cancellationToken);
        var interval = await dbContext.Intervals
            .SingleOrDefaultAsync(i => i.Code == kline.Interval, cancellationToken);

        if (symbol is null || interval is null)
        {
            logger.LogWarning(
                "Skipping closed kline — symbol {SymbolCode} or interval {IntervalCode} not found.",
                kline.Symbol,
                kline.Interval);
            return;
        }

        var existing = await dbContext.KlineData.SingleOrDefaultAsync(
            k => k.SymbolId == symbol.Id && k.IntervalId == interval.Id && k.OpenTime == kline.OpenTime,
            cancellationToken);

        if (existing is null)
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
        logger.LogInformation(
            "Stored closed kline {SymbolCode} {IntervalCode} {OpenTime}.",
            kline.Symbol,
            kline.Interval,
            kline.OpenTime);
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

    private static BinanceKlineStreamData? ParseKlineMessage(byte[] message)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (!root.TryGetProperty("k", out var kline))
            return null;

        var openTime = kline.GetProperty("t").GetInt64();
        var closeTime = kline.GetProperty("T").GetInt64();

        return new BinanceKlineStreamData(
            Type: "kline",
            Symbol: kline.GetProperty("s").GetString() ?? string.Empty,
            Interval: kline.GetProperty("i").GetString() ?? string.Empty,
            EventTime: root.GetProperty("E").GetInt64(),
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

    private static decimal GetDecimal(JsonElement element, string propertyName) =>
        decimal.Parse(
            element.GetProperty(propertyName).GetString() ?? "0",
            NumberStyles.Number,
            CultureInfo.InvariantCulture);

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
    }
}
