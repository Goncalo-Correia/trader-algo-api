using System.Net.Http.Json;
using System.Text.Json;

namespace TraderAlgoApi.Services.MarketData.Alpaca;

/// <summary>
/// Fetches historical bar data from the Alpaca Data API v2.
/// Implements IMarketDataProvider so DataCollectorService can use it without knowing about Alpaca.
/// </summary>
public sealed class AlpacaMarketDataProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration     configuration,
    ILogger<AlpacaMarketDataProvider> logger) : IMarketDataProvider
{
    private const string HttpClientName = "Alpaca";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int MaxPageSize => 10_000;

    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string          symbol,
        string          intervalCode,
        DateTimeOffset? startTime         = null,
        DateTimeOffset? endTime           = null,
        int?            limit             = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalCode);

        var timeframe = AlpacaIntervalMapper.ToAlpacaTimeframe(intervalCode);
        var feed      = configuration["Alpaca:Feed"] ?? "sip";
        var results   = new List<AlpacaBar>();
        string? pageToken = null;

        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        while (true)
        {
            var url = BuildUrl(symbol, timeframe, feed, startTime, endTime, limit, pageToken);

            logger.LogDebug("Fetching Alpaca bars: {Url}", url);

            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<AlpacaBarsResponse>(
                JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Null response from Alpaca bars endpoint.");

            if (page.Bars is { Count: > 0 })
                results.AddRange(page.Bars);

            if (string.IsNullOrEmpty(page.NextPageToken))
                break;

            // If a hard limit was requested stop after the first page.
            if (limit.HasValue)
                break;

            pageToken = page.NextPageToken;

            // Small delay to stay well inside Alpaca rate limits.
            await Task.Delay(100, cancellationToken);
        }

        return results
            .OrderBy(b => b.Timestamp)
            .Select(b => ToCandle(b, intervalCode))
            .ToArray();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildUrl(
        string          symbol,
        string          timeframe,
        string          feed,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int?            limit,
        string?         pageToken)
    {
        var query = new List<string>
        {
            $"timeframe={Uri.EscapeDataString(timeframe)}",
            $"feed={Uri.EscapeDataString(feed)}",
            "adjustment=raw",
        };

        if (startTime.HasValue)
            query.Add($"start={Uri.EscapeDataString(startTime.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}");

        if (endTime.HasValue)
            query.Add($"end={Uri.EscapeDataString(endTime.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}");

        if (limit.HasValue)
            query.Add($"limit={limit.Value}");

        if (!string.IsNullOrEmpty(pageToken))
            query.Add($"page_token={Uri.EscapeDataString(pageToken)}");

        return $"/v2/stocks/{Uri.EscapeDataString(symbol)}/bars?{string.Join('&', query)}";
    }

    private static Candle ToCandle(AlpacaBar bar, string intervalCode)
    {
        var duration  = IntervalDuration(intervalCode);
        var closeTime = bar.Timestamp.Add(duration).AddMilliseconds(-1);

        // Dollar volume: volume × VWAP (best available proxy for QuoteAssetVolume).
        var dollarVolume = bar.Volume * bar.Vwap;

        return new Candle(
            OpenTime:            bar.Timestamp,
            CloseTime:           closeTime,
            Open:                bar.Open,
            High:                bar.High,
            Low:                 bar.Low,
            Close:               bar.Close,
            Volume:              bar.Volume,
            QuoteAssetVolume:    dollarVolume,
            NumberOfTrades:      bar.NumberOfTrades,
            TakerBuyBaseVolume:  0m,
            TakerBuyQuoteVolume: 0m);
    }

    private static TimeSpan IntervalDuration(string intervalCode) => intervalCode switch
    {
        "1m"  => TimeSpan.FromMinutes(1),
        "5m"  => TimeSpan.FromMinutes(5),
        "15m" => TimeSpan.FromMinutes(15),
        "1h"  => TimeSpan.FromHours(1),
        "4h"  => TimeSpan.FromHours(4),
        "1d"  => TimeSpan.FromDays(1),
        _     => throw new ArgumentOutOfRangeException(nameof(intervalCode))
    };
}
