using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.DataCollector;
using TraderAlgoApi.Services.Kronos;
using TraderAlgoApi.Services.Session;
using TraderAlgoApi.WebSockets;

const string LocalDevelopmentCorsPolicy = "LocalDevelopmentCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<NyseSessionService>();
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
