using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TraderAlgoApi.Infrastructure;

/// <summary>
/// Centralizes how unhandled exceptions become HTTP responses so controllers don't each
/// hand-roll (and diverge on) the same try/catch → status-code mapping. Emits RFC 7807
/// ProblemDetails. Domain exceptions carry their message through; unexpected ones are logged
/// in full but return a generic message to the client.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The client went away mid-request — let the framework abort; there's nobody to write to.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var (status, title, exposeMessage) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogInformation("Request {Method} {Path} failed: {Message}",
                httpContext.Request.Method, httpContext.Request.Path, exception.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exposeMessage
                    ? exception.Message
                    : "An unexpected error occurred."
            }
        });
    }

    private static (int Status, string Title, bool ExposeMessage) Map(Exception exception) => exception switch
    {
        ArgumentException        => (StatusCodes.Status400BadRequest, "Invalid request", true),
        KeyNotFoundException     => (StatusCodes.Status404NotFound, "Not found", true),
        InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict", true),
        HttpRequestException     => (StatusCodes.Status503ServiceUnavailable, "Upstream service unavailable", true),
        _                        => (StatusCodes.Status500InternalServerError, "Internal server error", false)
    };
}
