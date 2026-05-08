using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Backtests;
using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.DataCollector;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.Kronos;
using TraderAlgoApi.Services.MarketData;
using TraderAlgoApi.Services.MarketData.Alpaca;
using TraderAlgoApi.Services.Ml;
using TraderAlgoApi.Services.PriceFeeds;
using TraderAlgoApi.Services.Session;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;
using TraderAlgoApi.Services.Rules.SmaMacd;
using TraderAlgoApi.Services.TradeBots;
using TraderAlgoApi.Services.TradeEvents;
using TraderAlgoApi.Services.Trades;
using TraderAlgoApi.Services.TradingAccounts;
using TraderAlgoApi.Services.TradingStrategies;
using TraderAlgoApi.WebSockets;

const string LocalDevelopmentCorsPolicy = "LocalDevelopmentCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalDevelopmentCorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "http://localhost:5111", "https://localhost:7096")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Supabase")));
builder.Services.AddSingleton(TimeProvider.System);

// ── Session / calendar ────────────────────────────────────────────────────────
builder.Services.AddSingleton<NyseSessionService>();

// ── Shared feeds ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<PriceFeed>();
builder.Services.AddSingleton<ClosedCandleFeed>();
builder.Services.AddSingleton<CandleAggregator>();

// ── Trade event publisher ─────────────────────────────────────────────────────
builder.Services.AddSingleton<ITradeEventPublisher, TradeEventPublisher>();

// ── Trading rules (stateless singletons) ─────────────────────────────────────
builder.Services.AddScoped<ITradingRuleContextService, TradingRuleContextService>();
builder.Services.AddSingleton<SmaTradingRule>();
builder.Services.AddSingleton<RsiTradingRule>();
builder.Services.AddSingleton<MacdTradingRule>();
builder.Services.AddSingleton<SmaMacdTradingRule>();

// ── Domain services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IBacktestService, BacktestService>();
builder.Services.AddScoped<IBacktestStreamService, BacktestStreamService>();
builder.Services.AddScoped<ITradeBotService, TradeBotService>();
builder.Services.AddScoped<ITradeBotSignalService, TradeBotSignalService>();
builder.Services.AddScoped<ITradeEventStreamService, TradeEventStreamService>();
builder.Services.AddScoped<ITradeService, TradeService>();
builder.Services.AddScoped<ITradingStrategyService, TradingStrategyService>();
builder.Services.AddScoped<ITradingAccountService, TradingAccountService>();

// ── Background hosted services ────────────────────────────────────────────────
builder.Services.AddHostedService<TradeMonitorService>();
builder.Services.AddHostedService<TradeBotMonitorService>();

// ── Indicator services ────────────────────────────────────────────────────────
builder.Services.AddScoped<ISimpleMovingAverageService, SimpleMovingAverageService>();
builder.Services.AddScoped<IRsiService, RsiService>();
builder.Services.AddScoped<IMacdService, MacdService>();
builder.Services.AddScoped<IIndicatorSyncService, IndicatorSyncService>();

// ── Data collection ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IBinanceDataCollectorService, BinanceDataCollectorService>();
builder.Services.AddScoped<IAlpacaDataCollectorService, AlpacaDataCollectorService>();
builder.Services.AddHostedService<DataCollectorTimer>();

// ── Live charts ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<ILiveChartDataService, LiveChartDataService>();

// ── Binance ───────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("Binance", client =>
{
    var baseUrl = builder.Configuration["Binance:BaseUrl"] ?? "https://api.binance.com";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
// BinanceMarketDataService implements both IBinanceMarketDataService and IMarketDataProvider.
builder.Services.AddScoped<BinanceMarketDataService>();
builder.Services.AddScoped<IBinanceMarketDataService>(sp => sp.GetRequiredService<BinanceMarketDataService>());
builder.Services.AddHostedService<BinanceKlineStreamingService>();

// ── Alpaca ────────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("Alpaca", client =>
{
    var baseUrl = builder.Configuration["Alpaca:BaseUrl"] ?? "https://data.alpaca.markets";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    var apiKey    = builder.Configuration["Alpaca:ApiKey"]    ?? string.Empty;
    var secretKey = builder.Configuration["Alpaca:SecretKey"] ?? string.Empty;
    client.DefaultRequestHeaders.Add("APCA-API-KEY-ID",     apiKey);
    client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", secretKey);
});
builder.Services.AddScoped<AlpacaMarketDataProvider>();
builder.Services.AddHostedService<AlpacaKlineStreamingService>();
builder.Services.AddHostedService<AlpacaRestPollingService>();

// ── Provider factory (resolves IMarketDataProvider per symbol) ────────────────
// BinanceMarketDataService (scoped) is injected as IMarketDataProvider via the factory.
builder.Services.AddScoped<IMarketDataProvider>(sp => sp.GetRequiredService<BinanceMarketDataService>());
builder.Services.AddScoped<IMarketDataProviderFactory>(sp =>
    new MarketDataProviderFactory(
        sp.GetRequiredService<BinanceMarketDataService>(),
        sp.GetRequiredService<AlpacaMarketDataProvider>()));

// ── Kronos ────────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("Kronos", client =>
{
    var baseUrl = builder.Configuration["Kronos:BaseUrl"] ?? "http://localhost:8765";
    client.BaseAddress = new Uri(baseUrl.TrimEnd());
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddScoped<IKronosConnectorService, KronosConnectorService>();
builder.Services.AddScoped<IKronosPredictService, KronosPredictService>();

// ── ML Policy ─────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("MlPolicy", client =>
{
    var baseUrl = builder.Configuration["MlPolicy:BaseUrl"] ?? "http://localhost:8766";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddSingleton(new MlConnectorOptions(
    ModelId: builder.Configuration["MlPolicy:ModelId"] ?? "ppo-v1"));
builder.Services.AddScoped<IMlConnectorService, MlConnectorService>();

var app = builder.Build();

app.UseWebSockets();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseWhen(
        context => !context.WebSockets.IsWebSocketRequest,
        applicationBuilder => applicationBuilder.UseHttpsRedirection());
}

app.UseCors(LocalDevelopmentCorsPolicy);
app.UseAuthorization();
app.MapWebSocketEndpoints();
app.MapControllers();

app.Run();
