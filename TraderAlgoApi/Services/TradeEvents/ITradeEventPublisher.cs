using TraderAlgoApi.Dtos.TradeEvents;

namespace TraderAlgoApi.Services.TradeEvents;

public interface ITradeEventPublisher
{
    void Publish(TradeEventDto tradeEvent);

    IAsyncEnumerable<TradeEventDto> SubscribeAsync(long? tradingAccountId, CancellationToken cancellationToken = default);
}
