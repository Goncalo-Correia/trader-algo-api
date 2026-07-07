using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Data;

/// <summary>
/// Reads the deterministic decision log from the <c>training_decisions</c> telemetry table the
/// Python ML sidecar writes. Previously this data was proxied from the sidecar over HTTP; it now
/// lives in the shared Postgres, so both the HTTP endpoint and the WebSocket replay read it here.
/// </summary>
public static class TrainingDecisionsQueryExtensions
{
    // The stored payload uses snake_case keys; MlTrainingDecisionsResponse carries explicit
    // [JsonPropertyName] attributes, so mapping is by attribute. Case-insensitive web defaults
    // are a harmless safety net.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Returns a run's decision log, or null if none has been written yet (training not finished,
    /// never run, or sidecar telemetry disabled). The DB <c>run_id</c> column is text; the route
    /// takes a long and compares as string, matching the other telemetry endpoints.
    /// </summary>
    public static async Task<MlTrainingDecisionsResponse?> GetTrainingDecisionLogAsync(
        this ApplicationDbContext dbContext,
        long trainingRunId,
        CancellationToken cancellationToken = default)
    {
        var key = trainingRunId.ToString();
        var payload = await dbContext.TrainingDecisionLogs
            .AsNoTracking()
            .Where(r => r.RunId == key)
            .Select(r => r.Payload)
            .FirstOrDefaultAsync(cancellationToken);

        return payload is null
            ? null
            : JsonSerializer.Deserialize<MlTrainingDecisionsResponse>(payload, JsonOptions);
    }
}
