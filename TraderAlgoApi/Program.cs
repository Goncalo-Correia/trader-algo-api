using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.Charts;
using TraderAlgoApi.Services.DataCollector;
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
builder.Services.AddScoped<IDataCollectorService, DataCollectorService>();
builder.Services.AddScoped<IChartsService, ChartsService>();
builder.Services.AddScoped<ILiveChartDataService, LiveChartDataService>();
builder.Services.AddScoped<IBinanceMarketDataWebSocketService, BinanceMarketDataWebSocketService>();
builder.Services.AddHttpClient<IBinanceMarketDataService, BinanceMarketDataService>(client =>
{
    var baseUrl = builder.Configuration["Binance:BaseUrl"] ?? "https://api.binance.com";

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

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
