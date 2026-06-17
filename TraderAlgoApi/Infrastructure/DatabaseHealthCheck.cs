using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TraderAlgoApi.Data;

namespace TraderAlgoApi.Infrastructure;

/// <summary>
/// Reports readiness by verifying the API can actually reach PostgreSQL. Uses the context
/// factory so the check runs on its own short-lived connection rather than a request scope.
/// </summary>
public sealed class DatabaseHealthCheck(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}
