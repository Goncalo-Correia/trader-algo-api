using System.Threading.Channels;
using TraderAlgoApi.Services.PriceFeeds;

namespace TraderAlgoApi.Services.Trades;

/// <summary>
/// Background service that subscribes to the <see cref="PriceFeed"/> and evaluates
/// pending limit fills and active SL/TP triggers on every incoming price tick.
/// A bounded channel with DropOldest mode absorbs burst ticks while ensuring only
/// the latest price for each symbol is acted on.
/// </summary>
public sealed class TradeMonitorService(
    PriceFeed priceFeed,
    IServiceScopeFactory scopeFactory,
    ILogger<TradeMonitorService> logger) : BackgroundService
{
    private readonly Channel<(string Symbol, decimal Price)> _channel =
        Channel.CreateBounded<(string, decimal)>(new BoundedChannelOptions(64)
        {
            FullMode      = BoundedChannelFullMode.DropOldest,
            SingleWriter  = false,
            SingleReader  = true
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        priceFeed.TickReceived += OnTick;
        try
        {
            await foreach (var (symbol, price) in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await EvaluateAsync(symbol, price, stoppingToken);
            }
        }
        finally
        {
            priceFeed.TickReceived -= OnTick;
            _channel.Writer.TryComplete();
        }
    }

    private void OnTick(string symbol, decimal price) =>
        _channel.Writer.TryWrite((symbol, price));

    private async Task EvaluateAsync(string symbol, decimal price, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITradeService>();
            await service.EvaluatePriceAsync(symbol, price, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error evaluating price tick for {Symbol} @ {Price}", symbol, price);
        }
    }
}
