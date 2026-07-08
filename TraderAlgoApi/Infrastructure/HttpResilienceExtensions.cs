using Microsoft.Extensions.Http.Resilience;

namespace TraderAlgoApi.Infrastructure;

/// <summary>
/// Shared resilience wiring for the outbound named HTTP clients (Binance, Kronos). Adds Polly-backed
/// retry with exponential backoff, a per-attempt and total-request timeout, and a circuit breaker so
/// a slow or failing upstream can't tie up request/background work or cascade into unrelated paths.
/// Only applied to idempotent calls — non-idempotent endpoints (e.g. ML <c>/train</c>) get a plain
/// timeout instead so a retry can't start a second job.
/// </summary>
public static class HttpResilienceExtensions
{
    public static void AddOutboundResilience(this IHttpClientBuilder builder) =>
        builder.AddStandardResilienceHandler(options =>
        {
            // Allow slower model inference / large kline pages before an attempt is abandoned.
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            // Circuit breaker sampling window must be at least twice the attempt timeout; keep a
            // margin above the 60s floor so option validation can't trip on the exact boundary.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(90);
        });
}
