using System.Collections.Concurrent;

namespace TraderAlgoApi.Services.PriceFeeds;

/// <summary>
/// Singleton in-memory price feed. The Binance streaming service publishes every
/// live tick here; trade monitoring subscribes via <see cref="TickReceived"/>.
/// </summary>
public sealed class PriceFeed
{
    private readonly ConcurrentDictionary<string, decimal> _latest = new();

    /// <summary>Fired on every incoming price tick (including unclosed candles).</summary>
    public event Action<string, decimal>? TickReceived;

    public void Publish(string symbol, decimal price)
    {
        _latest[symbol] = price;
        TickReceived?.Invoke(symbol, price);
    }

    /// <returns>The latest known price for the symbol, or null if none received yet.</returns>
    public decimal? GetLatestPrice(string symbol) =>
        _latest.TryGetValue(symbol, out var price) ? price : null;
}
