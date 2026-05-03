using TraderAlgoApi.Models;

namespace TraderAlgoApi.Services.TradeBots;

public interface ITradeBotSignalService
{
    Task<TradeBotSignalResult> EvaluateAsync(TradeBot tradeBot, CancellationToken cancellationToken = default);
}
