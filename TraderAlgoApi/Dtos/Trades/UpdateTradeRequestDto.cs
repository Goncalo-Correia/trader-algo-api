namespace TraderAlgoApi.Dtos.Trades;

public sealed record UpdateTradeRequestDto(
    decimal? StopLoss,
    decimal? TakeProfit);
