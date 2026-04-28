using System.Text.Json.Serialization;
using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Dtos.Trades;

public sealed record TradeResponseDto(
    [property: JsonPropertyName("id")]             long Id,
    [property: JsonPropertyName("symbolCode")]     string SymbolCode,
    [property: JsonPropertyName("intervalCode")]   string? IntervalCode,
    [property: JsonPropertyName("side")]           TradeSide Side,
    [property: JsonPropertyName("orderType")]      TradeOrderType OrderType,
    [property: JsonPropertyName("quantity")]       decimal Quantity,
    [property: JsonPropertyName("requestedPrice")] decimal? RequestedPrice,
    [property: JsonPropertyName("entryPrice")]     decimal? EntryPrice,
    [property: JsonPropertyName("stopLoss")]       decimal? StopLoss,
    [property: JsonPropertyName("takeProfit")]     decimal? TakeProfit,
    [property: JsonPropertyName("status")]         TradeStatus Status,
    [property: JsonPropertyName("createdAt")]      long CreatedAt,
    [property: JsonPropertyName("openedAt")]       long? OpenedAt,
    [property: JsonPropertyName("closedAt")]       long? ClosedAt,
    [property: JsonPropertyName("closedPrice")]    decimal? ClosedPrice,
    [property: JsonPropertyName("closeReason")]    TradeCloseReason? CloseReason);
