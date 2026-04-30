namespace TraderAlgoApi.Services.MarketData;

public sealed class ClosedCandleFeed
{
    public event Action<ClosedCandleEvent>? CandleClosed;

    public void Publish(ClosedCandleEvent candle) =>
        CandleClosed?.Invoke(candle);
}
