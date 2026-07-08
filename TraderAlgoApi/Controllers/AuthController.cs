using Microsoft.AspNetCore.Mvc;
using TraderAlgoApi.Dtos.Auth;
using TraderAlgoApi.Infrastructure;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(WebSocketTicketService ticketService) : ControllerBase
{
    /// <summary>
    /// Mints a short-lived, single-use ticket for opening a WebSocket connection. This request is
    /// itself gated by the normal <c>X-Api-Key</c> header, so only an authenticated caller can
    /// obtain a ticket; the browser then passes the ticket as <c>?ticket=</c> on the socket URL
    /// instead of putting the long-lived API key in the query string.
    /// </summary>
    [HttpPost("ws-ticket")]
    public ActionResult<WebSocketTicketResponse> CreateWebSocketTicket() =>
        Ok(new WebSocketTicketResponse(ticketService.Issue(), ticketService.LifetimeSeconds));
}
