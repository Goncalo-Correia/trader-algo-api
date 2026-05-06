using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Backtests;
using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.DataCollector;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.Kronos;
using TraderAlgoApi.Services.PriceFeeds;
using TraderAlgoApi.Services.Session;
using TraderAlgoApi.Services.Rules;
using TraderAlgoApi.Services.Rules.Macd;
using TraderAlgoApi.Services.Rules.Rsi;
using TraderAlgoApi.Services.Rules.Sma;
using TraderAlgoApi.Services.Rules.SmaMacd;
using TraderAlgoApi.Services.MarketData;
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
builder.Services.AddSingleton<NyseSessionService>();
builder.Services.AddSingleton<PriceFeed>();
builder.Services.AddSingleton<ClosedCandleFeed>();
builder.Services.AddSingleton<ITradeEventPublisher, TradeEventPublisher>();
builder.Services.AddScoped<ITradingRuleContextService, TradingRuleContextService>();
builder.Services.AddSingleton<SmaTradingRule>();
builder.Services.AddSingleton<RsiTradingRule>();
builder.Services.AddSingleton<MacdTradingRule>();
builder.Services.AddSingleton<SmaMacdTradingRule>();
builder.Services.AddScoped<IBacktestService, BacktestService>();
builder.Services.AddScoped<IBacktestStreamService, BacktestStreamService>();
builder.Services.AddScoped<ITradeBotService, TradeBotService>();
builder.Services.AddScoped<ITradeBotSignalService, TradeBotSignalService>();
builder.Services.AddScoped<ITradeEventStreamService, TradeEventStreamService>();
builder.Services.AddScoped<ITradeService, TradeService>();
builder.Services.AddScoped<ITradingStrategyService, TradingStrategyService>();
builder.Services.AddScoped<ITradingAccountService, TradingAccountService>();
builder.Services.AddHostedService<TradeMonitorService>();
builder.Services.AddHostedService<TradeBotMonitorService>();
builder.Services.AddScoped<ISimpleMovingAverageService, SimpleMovingAverageService>();
builder.Services.AddScoped<IRsiService, RsiService>();
builder.Services.AddScoped<IMacdService, MacdService>();
builder.Services.AddScoped<IIndicatorSyncService, IndicatorSyncService>();
builder.Services.AddScoped<IDataCollectorService, DataCollectorService>();
builder.Services.AddHostedService<DataCollectorTimer>();
builder.Services.AddScoped<ILiveChartDataService, LiveChartDataService>();
builder.Services.AddHttpClient("Binance", client =>
{
    var baseUrl = builder.Configuration["Binance:BaseUrl"] ?? "https://api.binance.com";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddScoped<IBinanceMarketDataService, BinanceMarketDataService>();
builder.Services.AddHostedService<BinanceKlineStreamingService>();
builder.Services.AddHttpClient("Kronos", client =>
{
    var baseUrl = builder.Configuration["Kronos:BaseUrl"] ?? "http://localhost:8765 ";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddScoped<IKronosConnectorService, KronosConnectorService>();
builder.Services.AddScoped<IKronosPredictService, KronosPredictService>();

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
