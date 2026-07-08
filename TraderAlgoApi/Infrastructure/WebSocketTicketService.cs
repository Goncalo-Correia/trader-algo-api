using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TraderAlgoApi.Infrastructure;

/// <summary>
/// Mints short-lived, single-use tickets for authenticating browser WebSocket handshakes. Browsers
/// can't attach an <c>X-Api-Key</c> header to a WebSocket upgrade, so the client first calls an
/// authenticated REST endpoint to obtain a ticket, then passes it as <c>?ticket=</c> on the socket
/// URL. Unlike the static API key, a ticket expires in seconds and is consumed on first use, so even
/// if it leaks into browser history or a proxy log it is worthless almost immediately and can't be
/// replayed.
/// </summary>
public sealed class WebSocketTicketService(TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    // ticket -> expiry. Bounded implicitly by the short lifetime + prune-on-issue.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tickets = new();

    public int LifetimeSeconds => (int)Lifetime.TotalSeconds;

    /// <summary>Issues a fresh ticket valid for <see cref="LifetimeSeconds"/> seconds.</summary>
    public string Issue()
    {
        PruneExpired();
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tickets[ticket] = timeProvider.GetUtcNow() + Lifetime;
        return ticket;
    }

    /// <summary>
    /// Validates and consumes a ticket. Returns true only for a ticket that exists and has not
    /// expired; the ticket is removed either way, so it can be redeemed at most once.
    /// </summary>
    public bool Redeem(string? ticket)
    {
        if (string.IsNullOrEmpty(ticket) || !_tickets.TryRemove(ticket, out var expiry))
            return false;

        return expiry >= timeProvider.GetUtcNow();
    }

    private void PruneExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in _tickets)
        {
            if (entry.Value < now)
                _tickets.TryRemove(entry.Key, out _);
        }
    }
}
