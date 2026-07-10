using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Npgsql;
using TraderAlgoApi.Data;
using TraderAlgoApi.Infrastructure;
using TraderAlgoApi.Services.Backtests;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.DataCollector;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.Jobs;
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

const string ApiCorsPolicy = "ApiCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSwaggerGen(options =>
{
    // Surface an "Authorize" button in the UI so the "Try it out" calls send the X-Api-Key header.
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "API key required to call the endpoints."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("ApiKey", document, null)] = new List<string>()
    });
});

// Centralized RFC 7807 error responses for everything not explicitly handled in a controller.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Allowed browser origins: localhost for dev, plus any configured for deployed
// front-ends. `Cors:AllowedOrigins` is a comma/semicolon-separated list (set via
// the `Cors__AllowedOrigins` env var on the host, or appsettings). Credentials
// aren't used — the API key travels in the X-Api-Key header, not cookies.
var allowedOrigins = new[]
    {
        "http://localhost:4200",
        "http://localhost:5111",
        "https://localhost:7096",
    }
    .Concat((builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(ApiCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Register both a scoped DbContext (for controllers/services) and a factory (for long-lived
// WebSocket streams and background work that must not hold a request-scoped context open).
var connectionString = BuildSupabaseConnectionString(builder.Configuration);
void ConfigureDb(DbContextOptionsBuilder options) =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null));
builder.Services.AddDbContext<ApplicationDbContext>(ConfigureDb);
builder.Services.AddDbContextFactory<ApplicationDbContext>(ConfigureDb, ServiceLifetime.Scoped);
builder.Services.AddDbContext<MlflowDbContext>(ConfigureDb);
builder.Services.AddDbContextFactory<MlflowDbContext>(ConfigureDb, ServiceLifetime.Scoped);

builder.Services.AddSingleton(TimeProvider.System);

// Mints short-lived single-use tickets so browser WebSocket handshakes don't put the API key in the
// query string. See Infrastructure/WebSocketTicketService and POST /api/auth/ws-ticket.
builder.Services.AddSingleton<WebSocketTicketService>();

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
// Single-flight background runner for backtest computation (decoupled from WebSocket clients).
builder.Services.AddSingleton<BacktestJobRunner>();
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
builder.Services.AddScoped<IAtrService, AtrService>();
builder.Services.AddScoped<IIndicatorSyncService, IndicatorSyncService>();

// ── Data collection ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IBinanceDataCollectorService, BinanceDataCollectorService>();
builder.Services.AddHostedService<DataCollectorTimer>();

// ── Background sync jobs (data collection / indicator recompute) ────────────────
builder.Services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();
builder.Services.AddSingleton<ISyncJobExecutor, SyncJobExecutor>();
builder.Services.AddScoped<ISyncJobService, SyncJobService>();
builder.Services.AddHostedService<SyncJobWorker>();

// ── Live charts ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<ILiveChartDataService, LiveChartDataService>();

// ── Market data providers (Binance + factory + streaming service) ──────────────
builder.Services.AddMarketDataProviders(builder.Configuration);

// ── Kronos ────────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("Kronos", client =>
{
    var baseUrl = builder.Configuration["Kronos:BaseUrl"] ?? "http://localhost:8765";
    client.BaseAddress = new Uri(baseUrl.TrimEnd());
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
})
// /predict is a stateless forecast (no side effects), so retry/backoff + circuit breaking are safe.
.AddOutboundResilience();
builder.Services.AddScoped<IKronosConnectorService, KronosConnectorService>();
builder.Services.AddScoped<IKronosPredictService, KronosPredictService>();

// ── ML Policy ─────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("MlPolicy", client =>
{
    var baseUrl = builder.Configuration["MlPolicy:BaseUrl"] ?? "http://localhost:8766";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

    // A plain request timeout (no retry handler): this client also serves the non-idempotent
    // /train endpoint, so an automatic retry could kick off a duplicate training run. The timeout
    // still bounds how long a hung sidecar can tie up a decide/train/models call.
    client.Timeout = TimeSpan.FromSeconds(120);

    // The ML sidecar gates every endpoint except /health behind an X-API-Key header when it is
    // configured with an API key (required when it is publicly reachable). Send the shared secret
    // on every call. Empty => the sidecar has auth disabled (local/private deploy), so omit it.
    var apiKey = builder.Configuration["MlPolicy:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey.Trim());
});
builder.Services.Configure<MlflowOptions>(builder.Configuration.GetSection("Mlflow"));
builder.Services.AddScoped<IMlConnectorService, MlConnectorService>();
builder.Services.AddScoped<IMlTrainingStreamService, MlTrainingStreamService>();
builder.Services.AddScoped<IMlflowTrackingRepository, MlflowTrackingRepository>();

var app = builder.Build();

// In development the schema churns between runs; clear Npgsql's prepared-statement cache so
// stale plans from before the latest migration don't cause column-count mismatches on startup.
// Not needed (and undesirable) in production, where the schema is stable.
if (app.Environment.IsDevelopment())
    NpgsqlConnection.ClearAllPools();

app.UseExceptionHandler();

app.UseWebSockets();

// Swagger is exposed in every environment by default; set Swagger:Enabled=false to turn it off.
// It maps your whole API surface, so gate it behind the API key (presented as the Basic-auth
// password, since a browser can't attach a custom header when navigating to /swagger).
if (builder.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwaggerApiKeyGate();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseWhen(
        context => !context.WebSockets.IsWebSocketRequest,
        applicationBuilder => applicationBuilder.UseHttpsRedirection());
}

app.UseCors(ApiCorsPolicy);

// Require the API key on all endpoints: REST via the X-Api-Key header; WebSockets via a short-lived
// single-use ?ticket= (from POST /api/auth/ws-ticket), with legacy ?apiKey= still accepted. Placed
// after CORS so the policy's headers are applied to 401 responses and preflight is handled first.
app.UseApiKeyAuthentication();

app.UseAuthorization();
app.MapWebSocketEndpoints();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static string BuildSupabaseConnectionString(IConfiguration configuration)
{
    var configured = configuration.GetConnectionString("Supabase");
    if (string.IsNullOrWhiteSpace(configured))
        throw new InvalidOperationException("ConnectionStrings:Supabase is required.");

    var builder = new NpgsqlConnectionStringBuilder(configured)
    {
        Pooling = true,
        GssEncryptionMode = GssEncryptionMode.Disable
    };

    var maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 5);
    if (maxPoolSize <= 0)
        throw new InvalidOperationException("Database:MaxPoolSize must be greater than zero.");

    builder.MaxPoolSize = maxPoolSize;

    var connectionIdleLifetime = configuration.GetValue("Database:ConnectionIdleLifetimeSeconds", 30);
    if (connectionIdleLifetime <= 0)
        throw new InvalidOperationException("Database:ConnectionIdleLifetimeSeconds must be greater than zero.");

    builder.ConnectionIdleLifetime = connectionIdleLifetime;

    var connectionPruningInterval = configuration.GetValue("Database:ConnectionPruningIntervalSeconds", 10);
    if (connectionPruningInterval <= 0)
        throw new InvalidOperationException("Database:ConnectionPruningIntervalSeconds must be greater than zero.");

    builder.ConnectionPruningInterval = connectionPruningInterval;

    var minPoolSize = configuration.GetValue<int?>("Database:MinPoolSize");
    if (minPoolSize is not null)
    {
        if (minPoolSize < 0 || minPoolSize > maxPoolSize)
            throw new InvalidOperationException("Database:MinPoolSize must be between zero and Database:MaxPoolSize.");

        builder.MinPoolSize = minPoolSize.Value;
    }

    return builder.ConnectionString;
}
