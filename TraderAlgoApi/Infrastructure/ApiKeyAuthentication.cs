using System.Security.Cryptography;
using System.Text;

namespace TraderAlgoApi.Infrastructure;

/// <summary>
/// Single-key API authentication for a single-user deployment. REST callers present the key in
/// the <c>X-Api-Key</c> header; clients that cannot set headers are handled separately —
/// WebSocket upgrades read the key from the query string, and Swagger page navigations use an
/// HTTP Basic-auth challenge. All comparisons hash both sides first so they run in constant time
/// regardless of key length, avoiding a timing side channel.
/// </summary>
public static class ApiKeyAuthentication
{
    private const string HeaderName = "X-Api-Key";
    private const string QueryName = "apiKey";
    private const string TicketQueryName = "ticket";

    /// <summary>Reads the configured key, failing fast at startup if it is missing.</summary>
    public static string GetRequiredApiKey(this IConfiguration configuration) =>
        configuration["ApiKey"] is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException(
                "ApiKey is not configured. Set it via user-secrets locally " +
                "(dotnet user-secrets set \"ApiKey\" \"<key>\") or an environment variable in production.");

    /// <summary>
    /// Enforces the API key on every request except CORS preflight, the health check, and the
    /// Swagger paths (which carry their own Basic-auth gate). WebSocket upgrades read the key from
    /// the <c>apiKey</c> query parameter because browsers cannot attach custom headers to a
    /// WebSocket handshake; everything else uses the <c>X-Api-Key</c> header.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this WebApplication app)
    {
        var apiKey = app.Configuration.GetRequiredApiKey();

        return app.Use(async (context, next) =>
        {
            if (IsExempt(context))
            {
                await next();
                return;
            }

            // WebSocket upgrades can't carry the X-Api-Key header, so they authenticate with a
            // short-lived single-use ticket (?ticket=), obtained from POST /api/auth/ws-ticket.
            // The legacy ?apiKey= query is still accepted as a fallback for un-migrated clients, but
            // it leaks the long-lived key into logs/history — prefer a ticket.
            var authorized = context.WebSockets.IsWebSocketRequest
                ? AuthorizeWebSocket(context, apiKey)
                : KeysMatch(context.Request.Headers[HeaderName].ToString(), apiKey);

            if (!authorized)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });
    }

    private static bool AuthorizeWebSocket(HttpContext context, string apiKey)
    {
        var ticketService = context.RequestServices.GetRequiredService<WebSocketTicketService>();
        if (ticketService.Redeem(context.Request.Query[TicketQueryName].ToString()))
            return true;

        // Legacy fallback: the raw API key in the query string.
        return KeysMatch(context.Request.Query[QueryName].ToString(), apiKey);
    }

    /// <summary>
    /// Gates the Swagger UI and document behind the same key using an HTTP Basic-auth challenge:
    /// a browser navigating to <c>/swagger</c> cannot send a custom header, so it is prompted for
    /// credentials and supplies the key as the password (the username is ignored). Runs on the
    /// main pipeline so non-Swagger requests pass straight through to the real Swagger middleware.
    /// </summary>
    public static IApplicationBuilder UseSwaggerApiKeyGate(this WebApplication app)
    {
        var apiKey = app.Configuration.GetRequiredApiKey();

        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                if (!TryReadBasicPassword(context, out var supplied) || !KeysMatch(supplied, apiKey))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Basic realm=\"Swagger\"";
                    return;
                }
            }

            await next();
        });
    }

    private static bool IsExempt(HttpContext context) =>
        HttpMethods.IsOptions(context.Request.Method) ||                 // CORS preflight carries no auth
        context.Request.Path.StartsWithSegments("/health") ||            // Render probes this without a key
        context.Request.Path.StartsWithSegments("/swagger");            // gated separately via Basic auth

    private static bool TryReadBasicPassword(HttpContext context, out string password)
    {
        password = string.Empty;
        string? header = context.Request.Headers.Authorization;
        if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        string decoded;
        try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim())); }
        catch (FormatException) { return false; }

        var separator = decoded.IndexOf(':');
        if (separator < 0)
            return false;

        password = decoded[(separator + 1)..];
        return true;
    }

    // Hash both sides to a fixed 32-byte length so the comparison is constant-time and does not
    // leak the key length. An empty/missing supplied value simply hashes to a non-matching digest.
    private static bool KeysMatch(string supplied, string expected)
    {
        Span<byte> suppliedHash = stackalloc byte[32];
        Span<byte> expectedHash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(supplied), suppliedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), expectedHash);
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
