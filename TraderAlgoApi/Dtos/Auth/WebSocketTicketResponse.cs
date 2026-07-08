namespace TraderAlgoApi.Dtos.Auth;

/// <summary>A short-lived, single-use ticket for authenticating a WebSocket handshake.</summary>
public sealed record WebSocketTicketResponse(string Ticket, int ExpiresInSeconds);
