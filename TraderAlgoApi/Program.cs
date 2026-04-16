using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.DataCollector;

const string LocalDevelopmentCorsPolicy = "LocalDevelopmentCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
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
builder.Services.AddScoped<IDataCollectorService, DataCollectorService>();
builder.Services.AddScoped<ILiveChartDataService, LiveChartDataService>();
builder.Services.AddScoped<IBinanceMarketDataWebSocketService, BinanceMarketDataWebSocketService>();
builder.Services.AddHttpClient<IBinanceMarketDataService, BinanceMarketDataService>(client =>
{
    var baseUrl = builder.Configuration["Binance:BaseUrl"] ?? "https://api.binance.com";

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.Map("/ws/binance/klines", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected a WebSocket request.");
        return;
    }

    var symbol = context.Request.Query["symbol"].FirstOrDefault() ?? "BTCUSDT";
    var interval = context.Request.Query["interval"].FirstOrDefault() ?? "1m";
    var streamService = context.RequestServices.GetRequiredService<IBinanceMarketDataWebSocketService>();

    using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
    await streamService.StreamKlinesAsync(clientSocket, symbol, interval, context.RequestAborted);
});

app.MapGet("/ws/charts/candles", async (
    HttpContext context,
    ILiveChartDataService liveChartDataService,
    CancellationToken cancellationToken) =>
{
    var symbol = context.Request.Query["symbol"].FirstOrDefault();
    var interval = context.Request.Query["interval"].FirstOrDefault();

    await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
})
.ExcludeFromDescription();

app.MapGet("/ws/charts/candles/{interval}", async (
    HttpContext context,
    string interval,
    ILiveChartDataService liveChartDataService,
    CancellationToken cancellationToken) =>
{
    var symbol = context.Request.Query["symbol"].FirstOrDefault();

    await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
})
.ExcludeFromDescription();

app.MapGet("/ws/charts/{symbol}/candles", async (
    HttpContext context,
    string symbol,
    ILiveChartDataService liveChartDataService,
    CancellationToken cancellationToken) =>
{
    var interval = context.Request.Query["interval"].FirstOrDefault();

    await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
})
.ExcludeFromDescription();

app.MapGet("/ws/charts/{symbol}/candles/{interval}", async (
    HttpContext context,
    string symbol,
    string interval,
    ILiveChartDataService liveChartDataService,
    CancellationToken cancellationToken) =>
{
    await liveChartDataService.StreamCandlesAsync(context, symbol, interval, cancellationToken);
})
.ExcludeFromDescription();

app.MapControllers();

app.Run();
