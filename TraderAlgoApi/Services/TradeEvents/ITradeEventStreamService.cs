namespace TraderAlgoApi.Services.TradeEvents;

public interface ITradeEventStreamService
{
    Task StreamAsync(HttpContext context, long? tradingAccountId, CancellationToken cancellationToken = default);
}
