using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.MarketData;
using TraderAlgoApi.Services.MarketData.Alpaca;

namespace TraderAlgoApi.Infrastructure;

/// <summary>
/// Groups the (otherwise sprawling) market-data provider wiring — HTTP clients, the
/// Binance/Alpaca services, their streaming background services, and the per-symbol
/// provider factory — behind one intent-revealing call.
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
        });

        // BinanceMarketDataService implements both IBinanceMarketDataService and IMarketDataProvider.
        services.AddScoped<BinanceMarketDataService>();
        services.AddScoped<IBinanceMarketDataService>(sp => sp.GetRequiredService<BinanceMarketDataService>());
        services.AddScoped<IMarketDataProvider>(sp => sp.GetRequiredService<BinanceMarketDataService>());
        services.AddHostedService<BinanceKlineStreamingService>();

        // ── Alpaca ────────────────────────────────────────────────────────────────
        services.AddHttpClient("Alpaca", client =>
        {
            var baseUrl = configuration["Alpaca:BaseUrl"] ?? "https://data.alpaca.markets";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var apiKey    = configuration["Alpaca:ApiKey"]    ?? string.Empty;
            var secretKey = configuration["Alpaca:SecretKey"] ?? string.Empty;
            client.DefaultRequestHeaders.Add("APCA-API-KEY-ID",     apiKey);
            client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", secretKey);
        });
        services.AddScoped<AlpacaMarketDataProvider>();
        services.AddHostedService<AlpacaKlineStreamingService>();
        services.AddHostedService<AlpacaRestPollingService>();

        // ── Provider factory (resolves IMarketDataProvider per symbol) ─────────────
        services.AddScoped<IMarketDataProviderFactory>(sp =>
            new MarketDataProviderFactory(
                sp.GetRequiredService<BinanceMarketDataService>(),
                sp.GetRequiredService<AlpacaMarketDataProvider>()));

        return services;
    }
}
