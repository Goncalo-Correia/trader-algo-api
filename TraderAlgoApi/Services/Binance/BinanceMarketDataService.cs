using System.Net.WebSockets;
using System.Text.Json;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.MarketData;

namespace TraderAlgoApi.Services.Binance;

public sealed class BinanceMarketDataService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<BinanceMarketDataService> logger,
    ISimpleMovingAverageService smaService,
    IRsiService rsiService,
    IMacdService macdService,
    IAtrService atrService) : IBinanceMarketDataService, IMarketDataProvider
{
    private const string HttpClientName = "Binance";
    private const string DefaultWebSocketBaseUrl = "wss://stream.binance.com:443";
    // Wilder's ATR averaging period; mirrors the value persisted by IndicatorSyncService.
    private const int AtrDefaultPeriod = 14;
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

    // ── IMarketDataProvider ───────────────────────────────────────────────────

    public int MaxPageSize => 1000;

    async Task<IReadOnlyList<Candle>> IMarketDataProvider.GetCandlesAsync(
        string symbol,
        string intervalCode,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int? limit,
        CancellationToken cancellationToken)
    {
        var klines = await GetKlinesAsync(symbol, intervalCode, startTime, endTime, limit, cancellationToken);
        return klines.Select(k => new Candle(
            OpenTime:           k.OpenTime,
            CloseTime:          k.CloseTime,
            Open:               k.Open,
            High:               k.High,
            Low:                k.Low,
            Close:              k.Close,
            Volume:             k.Volume,
            QuoteAssetVolume:   k.QuoteAssetVolume,
            NumberOfTrades:     k.NumberOfTrades,
            TakerBuyBaseVolume: k.TakerBuyBaseAssetVolume,
            TakerBuyQuoteVolume: k.TakerBuyQuoteAssetVolume)).ToArray();
    }

    // ── IBinanceMarketDataService ─────────────────────────────────────────────

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
                kline.Volume,
                kline.TakerBuyBaseAssetVolume,
                kline.Volume - kline.TakerBuyBaseAssetVolume),
            cancellationToken);
    }

    public async Task StreamKlineCandlesWithIndicatorsAsync(
        WebSocket clientSocket,
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        const int WindowSize = 200;

        var historicalKlines = await GetKlinesAsync(symbol, interval, limit: WindowSize, cancellationToken: cancellationToken);
        var closes = historicalKlines.Select(k => k.Close).ToList();
        var highs = historicalKlines.Select(k => k.High).ToList();
        var lows = historicalKlines.Select(k => k.Low).ToList();
        var lastKlineTime = historicalKlines.Count > 0
            ? historicalKlines[^1].OpenTime.ToUnixTimeSeconds()
            : 0L;

        await StreamKlinesInternalAsync(
            clientSocket,
            symbol,
            interval,
            kline =>
            {
                if (kline.Time == lastKlineTime)
                {
                    if (closes.Count > 0)
                    {
                        closes[^1] = kline.Close;
                        highs[^1] = kline.High;
                        lows[^1] = kline.Low;
                    }
                }
                else
                {
                    closes.Add(kline.Close);
                    highs.Add(kline.High);
                    lows.Add(kline.Low);
                    if (closes.Count > WindowSize)
                    {
                        closes.RemoveAt(0);
                        highs.RemoveAt(0);
                        lows.RemoveAt(0);
                    }
                    lastKlineTime = kline.Time;
                }

                var lastIndex = closes.Count - 1;

                var (sma20, sma100) = smaService.Compute(closes, lastIndex);

                var rsiValues = rsiService.ComputeAll(closes);
                var rsi = rsiValues[lastIndex];
                var rsiSmooth = rsiService.ComputeSmooth(rsiValues, lastIndex);
                var divergence = rsiService.DetectDivergence(closes, rsiValues, lastIndex);

                var macdValues = macdService.ComputeAll(closes);
                var (macdLine, signalLine, histogram) = macdValues[lastIndex];

                var atrValues = atrService.ComputeAll(highs, lows, closes);
                var (trueRange, atr) = atrValues[lastIndex];

                return new CandleWithIndicatorsResponseDto(
                    kline.Time,
                    kline.Open,
                    kline.High,
                    kline.Low,
                    kline.Close,
                    kline.Volume,
                    kline.TakerBuyBaseAssetVolume,
                    kline.Volume - kline.TakerBuyBaseAssetVolume,
                    sma20,
                    sma100,
                    rsi,
                    rsiSmooth,
                    divergence,
                    macdLine,
                    signalLine,
                    histogram,
                    AtrDefaultPeriod,
                    trueRange,
                    atr);
            },
            cancellationToken);
    }

    private async Task StreamKlinesInternalAsync<TResponse>(
        WebSocket clientSocket,
        string symbol,
        string interval,
        Func<BinanceKlineStream, TResponse> responseFactory,
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

                var streamData = BinanceKlineStream.FromJson(message);
                if (streamData is null)
                    continue;

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
