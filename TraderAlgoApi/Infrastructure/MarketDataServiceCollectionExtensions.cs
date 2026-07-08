using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.MarketData;

namespace TraderAlgoApi.Infrastructure;

/// <summary>
/// Groups the (otherwise sprawling) market-data provider wiring — HTTP clients, the
/// Binance service, its streaming background service, and the per-symbol provider
/// factory — behind one intent-revealing call.
/// </summary>
public static class MarketDataServiceCollectionExtensions
{
    public static IServiceCollection AddMarketDataProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Binance ───────────────────────────────────────────────────────────────
        services.AddHttpClient("Binance", client =>
        {
            var baseUrl = configuration["Binance:BaseUrl"] ?? "https://api.binance.com";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        // Kline reads are idempotent GETs, so retry/backoff + a circuit breaker are safe here and
        // keep a slow or rate-limiting Binance from tying up backfill/collector work indefinitely.
        .AddOutboundResilience();

        // BinanceMarketDataService implements both IBinanceMarketDataService and IMarketDataProvider.
        services.AddScoped<BinanceMarketDataService>();
        services.AddScoped<IBinanceMarketDataService>(sp => sp.GetRequiredService<BinanceMarketDataService>());
        services.AddScoped<IMarketDataProvider>(sp => sp.GetRequiredService<BinanceMarketDataService>());
        services.AddHostedService<BinanceKlineStreamingService>();

        // ── Provider factory (resolves IMarketDataProvider per symbol) ─────────────
        services.AddScoped<IMarketDataProviderFactory>(sp =>
            new MarketDataProviderFactory(
                sp.GetRequiredService<BinanceMarketDataService>()));

        return services;
    }
}
