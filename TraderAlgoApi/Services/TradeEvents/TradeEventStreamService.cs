using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraderAlgoApi.Services.TradeEvents;

public sealed class TradeEventStreamService(ITradeEventPublisher tradeEventPublisher) : ITradeEventStreamService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task StreamAsync(
        HttpContext context,
        long? tradingAccountId,
        CancellationToken cancellationToken = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsync(
                "This endpoint requires a WebSocket connection. Use ws:// or wss:// instead of http:// or https://.",
                cancellationToken);
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();

        await foreach (var tradeEvent in tradeEventPublisher.SubscribeAsync(tradingAccountId, cancellationToken))
        {
            if (socket.State != WebSocketState.Open)
                break;

            var payload = JsonSerializer.Serialize(tradeEvent, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(payload);

            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
    }
}
