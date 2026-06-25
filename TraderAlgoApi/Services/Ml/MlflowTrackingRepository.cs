using System.Net;
using Microsoft.Extensions.Options;
using Npgsql;
using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Services.Ml;

public sealed class MlflowTrackingRepository(
    IOptions<MlflowOptions> options,
    IConfiguration configuration,
    ILogger<MlflowTrackingRepository> logger) : IMlflowTrackingRepository
{
    private const string TrainingRunParamKey = "training_run_id";

    public async Task<MlflowTrainingTrackingResponse> GetTrackingAsync(
        long trainingRunId,
        bool includeMetricHistory,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetConnectionString(out var connectionString, out var message))
            return MlflowTrainingTrackingResponse.Unavailable(trainingRunId, message);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var runs = await LoadRunRowsAsync(connection, trainingRunId, cancellationToken);
            if (runs.Count == 0)
                return MlflowTrainingTrackingResponse.Unavailable(
                    trainingRunId,
                    "Tracking data not available yet.");

            if (runs.Count > 1)
                logger.LogWarning(
                    "Multiple MLflow runs are linked to app training run {TrainingRunId}; using latest run {RunUuid}",
                    trainingRunId, runs[0].RunUuid);

            var run = runs[0];
            var runUuids = new[] { run.RunUuid };
            var parameters = await LoadParamsAsync(connection, runUuids, cancellationToken);
            var latestMetrics = await LoadLatestMetricsAsync(connection, runUuids, cancellationToken);
            var metricHistory = includeMetricHistory
                ? await LoadMetricHistoryAsync(connection, run.RunUuid, cancellationToken)
                : new Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>();

            return BuildResponse(
                trainingRunId,
                run,
                parameters.GetValueOrDefault(run.RunUuid) ?? new Dictionary<string, string>(),
                latestMetrics.GetValueOrDefault(run.RunUuid) ?? new Dictionary<string, double?>(),
                metricHistory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Unable to load MLflow tracking data for training run {TrainingRunId}", trainingRunId);
            return MlflowTrainingTrackingResponse.Unavailable(
                trainingRunId,
                "MLflow tracking data unavailable.");
        }
    }

    public async Task<IReadOnlyDictionary<long, MlflowTrainingTrackingSummaryDto>> GetTrackingSummariesAsync(
        IReadOnlyCollection<long> trainingRunIds,
        CancellationToken cancellationToken = default)
    {
        var ids = trainingRunIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<long, MlflowTrainingTrackingSummaryDto>();

        if (!TryGetConnectionString(out var connectionString, out var message))
            return ids.ToDictionary(id => id, _ => MlflowTrainingTrackingSummaryDto.Unavailable(message));

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var runs = await LoadRunRowsAsync(connection, ids, cancellationToken);
            var latestRuns = runs
                .GroupBy(r => r.TrainingRunId)
                .ToDictionary(g => g.Key, g =>
                {
                    var matches = g.ToList();
                    if (matches.Count > 1)
                    {
                        logger.LogWarning(
                            "Multiple MLflow runs are linked to app training run {TrainingRunId}; using latest run {RunUuid}",
                            g.Key, matches[0].RunUuid);
                    }

                    return matches[0];
                });

            var runUuids = latestRuns.Values.Select(r => r.RunUuid).ToArray();
            var parameters = await LoadParamsAsync(connection, runUuids, cancellationToken);
            var latestMetrics = await LoadLatestMetricsAsync(connection, runUuids, cancellationToken);

            var result = new Dictionary<long, MlflowTrainingTrackingSummaryDto>();
            foreach (var id in ids)
            {
                if (!latestRuns.TryGetValue(id, out var run))
                {
                    result[id] = MlflowTrainingTrackingSummaryDto.Unavailable(
                        "Tracking data not available yet.");
                    continue;
                }

                result[id] = BuildResponse(
                    id,
                    run,
                    parameters.GetValueOrDefault(run.RunUuid) ?? new Dictionary<string, string>(),
                    latestMetrics.GetValueOrDefault(run.RunUuid) ?? new Dictionary<string, double?>(),
                    new Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>())
                    .ToSummary();
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Unable to load MLflow tracking summaries");
            return ids.ToDictionary(
                id => id,
                _ => MlflowTrainingTrackingSummaryDto.Unavailable("MLflow tracking data unavailable."));
        }
    }

    private bool TryGetConnectionString(out string connectionString, out string? message)
    {
        if (!options.Value.Enabled)
        {
            connectionString = string.Empty;
            message = "MLflow tracking is disabled.";
            return false;
        }

        var configured = FirstConfigured(
            options.Value.ConnectionString,
            configuration.GetConnectionString("Mlflow"),
            options.Value.TrackingUri,
            configuration["MLFLOW_TRACKING_URI"],
            configuration.GetConnectionString("Supabase"));

        if (!string.IsNullOrWhiteSpace(configured))
            return TryNormalizeConnectionString(configured, out connectionString, out message);

        connectionString = string.Empty;
        message = "MLflow connection string is not configured.";
        return false;
    }

    private static string? FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool TryNormalizeConnectionString(
        string configured,
        out string connectionString,
        out string? message)
    {
        var trimmed = configured.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (IsPostgresUri(uri))
            {
                connectionString = BuildNpgsqlConnectionString(uri);
                message = null;
                return true;
            }

            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = string.Empty;
                message = "MLflow tracking must point to a PostgreSQL tracking database for API reads.";
                return false;
            }
        }

        connectionString = trimmed;
        message = null;
        return true;
    }

    private static bool IsPostgresUri(Uri uri) =>
        uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase);

    private static string BuildNpgsqlConnectionString(Uri uri)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            SslMode = GetSslMode(uri)
        };

        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (!string.IsNullOrWhiteSpace(database))
            builder.Database = database;

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0 && !string.IsNullOrWhiteSpace(userInfo[0]))
            builder.Username = WebUtility.UrlDecode(userInfo[0]);
        if (userInfo.Length > 1)
            builder.Password = WebUtility.UrlDecode(userInfo[1]);

        return builder.ConnectionString;
    }

    private static SslMode GetSslMode(Uri uri)
    {
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
            return SslMode.Require;

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length != 2 ||
                !pieces[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return WebUtility.UrlDecode(pieces[1]).ToLowerInvariant() switch
            {
                "disable" => SslMode.Disable,
                "allow" => SslMode.Allow,
                "prefer" => SslMode.Prefer,
                "verify-ca" => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                _ => SslMode.Require
            };
        }

        return SslMode.Require;
    }

    private static async Task<List<MlflowRunRow>> LoadRunRowsAsync(
        NpgsqlConnection connection,
        long trainingRunId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.value AS training_run_id,
                   r.run_uuid,
                   r.name,
                   r.status,
                   r.start_time,
                   r.end_time,
                   r.artifact_uri
            FROM runs r
            JOIN params p ON p.run_uuid = r.run_uuid
            WHERE p.key = @paramKey
              AND p.value = @trainingRunIdText
            ORDER BY r.start_time DESC NULLS LAST
            LIMIT 2;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("paramKey", TrainingRunParamKey);
        command.Parameters.AddWithValue("trainingRunIdText", trainingRunId.ToString());

        return await ReadRunRowsAsync(command, cancellationToken);
    }

    private static async Task<List<MlflowRunRow>> LoadRunRowsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<long> trainingRunIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.value AS training_run_id,
                   r.run_uuid,
                   r.name,
                   r.status,
                   r.start_time,
                   r.end_time,
                   r.artifact_uri
            FROM runs r
            JOIN params p ON p.run_uuid = r.run_uuid
            WHERE p.key = @paramKey
              AND p.value = ANY(@trainingRunIds)
            ORDER BY p.value, r.start_time DESC NULLS LAST;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("paramKey", TrainingRunParamKey);
        command.Parameters.AddWithValue(
            "trainingRunIds",
            trainingRunIds.Select(id => id.ToString()).ToArray());

        return await ReadRunRowsAsync(command, cancellationToken);
    }

    private static async Task<List<MlflowRunRow>> ReadRunRowsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<MlflowRunRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!long.TryParse(reader.GetString(0), out var trainingRunId))
                continue;

            rows.Add(new MlflowRunRow(
                TrainingRunId: trainingRunId,
                RunUuid: reader.GetString(1),
                RunName: reader.IsDBNull(2) ? null : reader.GetString(2),
                Status: reader.IsDBNull(3) ? null : reader.GetString(3),
                StartTime: reader.IsDBNull(4) ? null : FromUnixMilliseconds(reader.GetInt64(4)),
                EndTime: reader.IsDBNull(5) ? null : FromUnixMilliseconds(reader.GetInt64(5)),
                ArtifactUri: reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private static async Task<Dictionary<string, Dictionary<string, string>>> LoadParamsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<string> runUuids,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (runUuids.Count == 0)
            return result;

        const string sql = """
            SELECT run_uuid, key, value
            FROM params
            WHERE run_uuid = ANY(@runUuids)
            ORDER BY key;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("runUuids", runUuids.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var runUuid = reader.GetString(0);
            if (!result.TryGetValue(runUuid, out var parameters))
            {
                parameters = new Dictionary<string, string>(StringComparer.Ordinal);
                result[runUuid] = parameters;
            }

            parameters[reader.GetString(1)] = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        }

        return result;
    }

    private static async Task<Dictionary<string, Dictionary<string, double?>>> LoadLatestMetricsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<string> runUuids,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Dictionary<string, double?>>();
        if (runUuids.Count == 0)
            return result;

        const string sql = """
            SELECT run_uuid, key, value, COALESCE(is_nan, false) AS is_nan
            FROM latest_metrics
            WHERE run_uuid = ANY(@runUuids)
            ORDER BY key;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("runUuids", runUuids.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var isNan = reader.GetBoolean(3);
            if (isNan)
                continue;

            var value = reader.GetDouble(2);
            if (double.IsNaN(value))
                continue;

            var runUuid = reader.GetString(0);
            if (!result.TryGetValue(runUuid, out var metrics))
            {
                metrics = new Dictionary<string, double?>(StringComparer.Ordinal);
                result[runUuid] = metrics;
            }

            metrics[reader.GetString(1)] = value;
        }

        return result;
    }

    private static async Task<Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>> LoadMetricHistoryAsync(
        NpgsqlConnection connection,
        string runUuid,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT key, value, timestamp, step, COALESCE(is_nan, false) AS is_nan
            FROM metrics
            WHERE run_uuid = @runUuid
            ORDER BY key, step, timestamp;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("runUuid", runUuid);

        var grouped = new Dictionary<string, List<MlflowMetricPointDto>>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var isNan = reader.GetBoolean(4);
            if (isNan)
                continue;

            var value = reader.GetDouble(1);
            if (double.IsNaN(value))
                continue;

            var key = reader.GetString(0);
            if (!grouped.TryGetValue(key, out var points))
            {
                points = [];
                grouped[key] = points;
            }

            points.Add(new MlflowMetricPointDto(
                Step: reader.GetInt64(3),
                Value: value,
                Timestamp: FromUnixMilliseconds(reader.GetInt64(2))));
        }

        return grouped.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MlflowMetricPointDto>)pair.Value);
    }

    private static MlflowTrainingTrackingResponse BuildResponse(
        long trainingRunId,
        MlflowRunRow run,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, double?> latestMetrics,
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory) =>
        new(
            TrainingRunId: trainingRunId,
            TrackingAvailable: true,
            MlflowRunUuid: run.RunUuid,
            RunName: run.RunName,
            Status: run.Status,
            StartTime: run.StartTime,
            EndTime: run.EndTime,
            ArtifactUri: run.ArtifactUri,
            Params: parameters,
            RewardMetrics: MlflowRewardTrackingDashboardDto.From(latestMetrics, metricHistory),
            LatestMetrics: latestMetrics,
            MetricHistory: metricHistory);

    private static DateTimeOffset FromUnixMilliseconds(long timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

    private sealed record MlflowRunRow(
        long TrainingRunId,
        string RunUuid,
        string? RunName,
        string? Status,
        DateTimeOffset? StartTime,
        DateTimeOffset? EndTime,
        string? ArtifactUri);
}
