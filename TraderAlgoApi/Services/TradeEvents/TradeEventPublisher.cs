using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TraderAlgoApi.Dtos.TradeEvents;

namespace TraderAlgoApi.Services.TradeEvents;

public sealed class TradeEventPublisher : ITradeEventPublisher
{
    private sealed record Subscriber(long? TradingAccountId, Channel<TradeEventDto> Channel);

    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();

    public void Publish(TradeEventDto tradeEvent)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            if (subscriber.TradingAccountId is long accountId &&
                tradeEvent.TradingAccountId != accountId)
            {
                continue;
            }

            subscriber.Channel.Writer.TryWrite(tradeEvent);
        }
    }

    public async IAsyncEnumerable<TradeEventDto> SubscribeAsync(
        long? tradingAccountId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<TradeEventDto>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = new Subscriber(tradingAccountId, channel);

        try
        {
            await foreach (var tradeEvent in channel.Reader.ReadAllAsync(cancellationToken))
                yield return tradeEvent;
        }
        finally
        {
            if (_subscribers.TryRemove(id, out var subscriber))
                subscriber.Channel.Writer.TryComplete();
        }
    }
}
