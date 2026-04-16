using System.Text.Json;

namespace TraderAlgoApi.Services.Binance;

public sealed class BinanceMarketDataService(HttpClient httpClient) : IBinanceMarketDataService
{
    public async Task<IReadOnlyList<BinanceKline>> getKlines(
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
            $"symbol={Uri.EscapeDataString(NormalizeSymbol(symbol))}",
            $"interval={Uri.EscapeDataString(interval.Trim())}"
        };

        if (startTime is not null)
        {
            queryParameters.Add($"startTime={startTime.Value.ToUnixTimeMilliseconds()}");
        }

        if (endTime is not null)
        {
            queryParameters.Add($"endTime={endTime.Value.ToUnixTimeMilliseconds()}");
        }

        if (limit is not null)
        {
            queryParameters.Add($"limit={limit.Value}");
        }

        var requestUri = $"/api/v3/klines?{string.Join('&', queryParameters)}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind is not JsonValueKind.Array)
        {
            throw new JsonException("Unexpected Binance klines response.");
        }

        return document.RootElement
            .EnumerateArray()
            .Select(BinanceKline.FromJsonArray)
            .ToArray();
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
            ? "BTCUSDT"
            : normalizedSymbol;
    }
}
