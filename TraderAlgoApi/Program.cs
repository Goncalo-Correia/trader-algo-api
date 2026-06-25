using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TraderAlgoApi.Data;
using TraderAlgoApi.Infrastructure;
using TraderAlgoApi.Services.Backtests;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.DataCollector;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.Kronos;
using TraderAlgoApi.Services.MarketData;
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

// Centralized RFC 7807 error responses for everything not explicitly handled in a controller.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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

// Register both a scoped DbContext (for controllers/services) and a factory (for long-lived
// WebSocket streams and background work that must not hold a request-scoped context open).
var connectionString = builder.Configuration.GetConnectionString("Supabase");
void ConfigureDb(DbContextOptionsBuilder options) => options.UseNpgsql(connectionString);
builder.Services.AddDbContext<ApplicationDbContext>(ConfigureDb);
builder.Services.AddDbContextFactory<ApplicationDbContext>(ConfigureDb, ServiceLifetime.Scoped);

builder.Services.AddSingleton(TimeProvider.System);

// ── Health checks ───────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

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

// ── Market data providers (Binance + Alpaca + factory + streaming services) ────
builder.Services.AddMarketDataProviders(builder.Configuration);

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
builder.Services.AddScoped<IMlConnectorService, MlConnectorService>();
builder.Services.AddScoped<IMlTrainingStreamService, MlTrainingStreamService>();

var app = builder.Build();

// In development the schema churns between runs; clear Npgsql's prepared-statement cache so
// stale plans from before the latest migration don't cause column-count mismatches on startup.
// Not needed (and undesirable) in production, where the schema is stable.
if (app.Environment.IsDevelopment())
    NpgsqlConnection.ClearAllPools();

app.UseExceptionHandler();

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
app.MapHealthChecks("/health");

app.Run();
