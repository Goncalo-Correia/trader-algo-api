using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Services.Ml;

public sealed class MlflowTrackingRepository(
    IOptions<MlflowOptions> options,
    IConfiguration configuration,
    MlflowDbContext mlflowDb,
    ILogger<MlflowTrackingRepository> logger) : IMlflowTrackingRepository
{
    private const string TrainingRunParamKey = "training_run_id";
    private const string ModelIdParamKey = "model_id";

    // ── Public interface ───────────────────────────────────────────────────────

    public async Task<MlflowTrainingTrackingResponse> GetTrackingAsync(
        long trainingRunId,
        bool includeMetricHistory,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return MlflowTrainingTrackingResponse.Unavailable(trainingRunId, "MLflow tracking is disabled.");

        try
        {
            var run = await LoadRunEfAsync(trainingRunId, cancellationToken);
            if (run is null)
                return MlflowTrainingTrackingResponse.Unavailable(trainingRunId, "Tracking data not available yet.");

            // Wave 1 — parallel queries that only need the run UUID
            var paramsTask         = LoadParamsEfAsync(run.RunUuid, cancellationToken);
            var latestMetricsTask  = LoadLatestMetricsEfAsync(run.RunUuid, cancellationToken);
            var metricHistoryTask  = includeMetricHistory
                ? LoadMetricHistoryEfAsync(run.RunUuid, cancellationToken)
                : Task.FromResult<Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>>(new());
            var tagsTask           = LoadTagsAsync(run.RunUuid, cancellationToken);
            var experimentTask     = LoadExperimentAsync(run.ExperimentId, cancellationToken);

            await Task.WhenAll(paramsTask, latestMetricsTask, metricHistoryTask, tagsTask, experimentTask);

            var parameters    = await paramsTask;
            var latestMetrics = await latestMetricsTask;
            var metricHistory = await metricHistoryTask;
            var tags          = await tagsTask;
            var experiment    = await experimentTask;

            // Wave 2 — registry lookup requires model_id from params
            MlflowModelRegistryDto? registry = null;
            if (parameters.TryGetValue(ModelIdParamKey, out var modelId) &&
                !string.IsNullOrWhiteSpace(modelId))
            {
                registry = await LoadRegistryAsync(modelId, run.RunUuid, cancellationToken);
            }

            return BuildResponse(
                trainingRunId, run, parameters, latestMetrics, metricHistory,
                tags, experiment, registry);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Unable to load MLflow tracking data for training run {TrainingRunId}", trainingRunId);
            return MlflowTrainingTrackingResponse.Unavailable(trainingRunId, "MLflow tracking data unavailable.");
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
                    result[id] = MlflowTrainingTrackingSummaryDto.Unavailable("Tracking data not available yet.");
                    continue;
                }

                result[id] = BuildResponse(
                    id,
                    run,
                    parameters.GetValueOrDefault(run.RunUuid) ?? new Dictionary<string, string>(),
                    latestMetrics.GetValueOrDefault(run.RunUuid) ?? new Dictionary<string, double?>(),
                    new Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>(),
                    tags: null,
                    experiment: null,
                    registry: null)
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

    // ── EF Core query methods (used by GetTrackingAsync) ──────────────────────

    private async Task<MlflowRunRow?> LoadRunEfAsync(
        long trainingRunId,
        CancellationToken cancellationToken)
    {
        var trainingRunIdText = trainingRunId.ToString();

        var row = await mlflowDb.Runs
            .AsNoTracking()
            .Join(
                mlflowDb.Params.AsNoTracking()
                    .Where(p => p.Key == TrainingRunParamKey && p.Value == trainingRunIdText),
                run => run.RunUuid,
                param => param.RunUuid,
                (run, _) => run)
            .OrderByDescending(r => r.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        if (await mlflowDb.Runs
                .AsNoTracking()
                .Join(
                    mlflowDb.Params.AsNoTracking()
                        .Where(p => p.Key == TrainingRunParamKey && p.Value == trainingRunIdText),
                    run => run.RunUuid,
                    param => param.RunUuid,
                    (run, _) => run)
                .CountAsync(cancellationToken) > 1)
        {
            logger.LogWarning(
                "Multiple MLflow runs are linked to app training run {TrainingRunId}; using latest run {RunUuid}",
                trainingRunId, row.RunUuid);
        }

        return new MlflowRunRow(
            TrainingRunId: trainingRunId,
            RunUuid: row.RunUuid,
            RunName: row.Name,
            Status: row.Status,
            StartTime: row.StartTime is long s ? FromUnixMilliseconds(s) : null,
            EndTime: row.EndTime is long e ? FromUnixMilliseconds(e) : null,
            ArtifactUri: row.ArtifactUri,
            ExperimentId: row.ExperimentId);
    }

    private async Task<Dictionary<string, string>> LoadParamsEfAsync(
        string runUuid,
        CancellationToken cancellationToken)
    {
        var rows = await mlflowDb.Params
            .AsNoTracking()
            .Where(p => p.RunUuid == runUuid)
            .OrderBy(p => p.Key)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            p => p.Key,
            p => p.Value ?? string.Empty,
            StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, double?>> LoadLatestMetricsEfAsync(
        string runUuid,
        CancellationToken cancellationToken)
    {
        var rows = await mlflowDb.LatestMetrics
            .AsNoTracking()
            .Where(m => m.RunUuid == runUuid && !m.IsNan)
            .OrderBy(m => m.Key)
            .ToListAsync(cancellationToken);

        return rows
            .Where(m => !double.IsNaN(m.Value))
            .ToDictionary(m => m.Key, m => (double?)m.Value, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>> LoadMetricHistoryEfAsync(
        string runUuid,
        CancellationToken cancellationToken)
    {
        var rows = await mlflowDb.Metrics
            .AsNoTracking()
            .Where(m => m.RunUuid == runUuid && !m.IsNan)
            .OrderBy(m => m.Key)
            .ThenBy(m => m.Step)
            .ThenBy(m => m.Timestamp)
            .ToListAsync(cancellationToken);

        var grouped = new Dictionary<string, List<MlflowMetricPointDto>>(StringComparer.Ordinal);
        foreach (var m in rows)
        {
            if (double.IsNaN(m.Value))
                continue;

            if (!grouped.TryGetValue(m.Key, out var points))
            {
                points = [];
                grouped[m.Key] = points;
            }

            points.Add(new MlflowMetricPointDto(
                Step: m.Step,
                Value: m.Value,
                Timestamp: FromUnixMilliseconds(m.Timestamp)));
        }

        return grouped.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MlflowMetricPointDto>)pair.Value);
    }

    private async Task<MlflowRunTagsDto?> LoadTagsAsync(
        string runUuid,
        CancellationToken cancellationToken)
    {
        string[] knownKeys = ["mlflow.user", "mlflow.source.name", "mlflow.source.type"];

        var rows = await mlflowDb.Tags
            .AsNoTracking()
            .Where(t => t.RunUuid == runUuid && knownKeys.Contains(t.Key))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return null;

        var lookup = rows.ToDictionary(t => t.Key, t => t.Value);
        return new MlflowRunTagsDto(
            User:       lookup.GetValueOrDefault("mlflow.user"),
            SourceName: lookup.GetValueOrDefault("mlflow.source.name"),
            SourceType: lookup.GetValueOrDefault("mlflow.source.type"));
    }

    private async Task<MlflowExperimentInfoDto?> LoadExperimentAsync(
        int? experimentId,
        CancellationToken cancellationToken)
    {
        if (experimentId is null)
            return null;

        var exp = await mlflowDb.Experiments
            .AsNoTracking()
            .Where(e => e.ExperimentId == experimentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (exp is null)
            return null;

        return new MlflowExperimentInfoDto(
            ExperimentId:   exp.ExperimentId,
            Name:           exp.Name,
            LifecycleStage: exp.LifecycleStage,
            CreationTime:   exp.CreationTime is long ct ? FromUnixMilliseconds(ct) : null);
    }

    private async Task<MlflowModelRegistryDto?> LoadRegistryAsync(
        string modelName,
        string runUuid,
        CancellationToken cancellationToken)
    {
        var registeredModelTask = mlflowDb.RegisteredModels
            .AsNoTracking()
            .Where(m => m.Name == modelName)
            .FirstOrDefaultAsync(cancellationToken);

        var versionsTask = mlflowDb.ModelVersions
            .AsNoTracking()
            .Where(v => v.Name == modelName)
            .OrderBy(v => v.Version)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(registeredModelTask, versionsTask);

        var registeredModel = await registeredModelTask;
        if (registeredModel is null)
            return null;

        var versions = await versionsTask;
        var versionDtos = versions.Select(v => new MlflowModelVersionDto(
            Version:         v.Version,
            CurrentStage:    v.CurrentStage,
            Source:          v.Source,
            StorageLocation: v.StorageLocation,
            CreationTime:    v.CreationTime is long ct ? FromUnixMilliseconds(ct) : null,
            Description:     v.Description,
            RunId:           v.RunId)).ToList();

        var thisRunVersion = versionDtos.FirstOrDefault(v =>
            string.Equals(v.RunId, runUuid, StringComparison.OrdinalIgnoreCase));

        return new MlflowModelRegistryDto(
            ModelName:        registeredModel.Name,
            ModelDescription: registeredModel.Description,
            RegisteredAt:     registeredModel.CreationTime is long rt ? FromUnixMilliseconds(rt) : null,
            ThisRunVersion:   thisRunVersion,
            AllVersions:      versionDtos);
    }

    // ── Response builder ───────────────────────────────────────────────────────

    private static MlflowTrainingTrackingResponse BuildResponse(
        long trainingRunId,
        MlflowRunRow run,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, double?> latestMetrics,
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory,
        MlflowRunTagsDto? tags,
        MlflowExperimentInfoDto? experiment,
        MlflowModelRegistryDto? registry)
    {
        var evalMetrics = latestMetrics
            .Where(kv => kv.Key.StartsWith("eval/", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var ppoInternals = new MlflowPpoInternalsDto(
            PolicyGradientLoss: Metric(latestMetrics, metricHistory,
                "Policy gradient loss", "train/policy_gradient_loss"),
            ValueLoss: Metric(latestMetrics, metricHistory,
                "Value loss", "train/value_loss"),
            EntropyLoss: Metric(latestMetrics, metricHistory,
                "Entropy loss", "train/entropy_loss"),
            ApproxKl: Metric(latestMetrics, metricHistory,
                "Approx KL", "train/approx_kl"),
            ClipFraction: Metric(latestMetrics, metricHistory,
                "Clip fraction", "train/clip_fraction"),
            ExplainedVariance: Metric(latestMetrics, metricHistory,
                "Explained variance", "train/explained_variance"),
            EpRewMean: Metric(latestMetrics, metricHistory,
                "Episode reward mean", "rollout/ep_rew_mean"),
            EpLenMean: Metric(latestMetrics, metricHistory,
                "Episode length mean", "rollout/ep_len_mean"));

        return new MlflowTrainingTrackingResponse(
            TrainingRunId:    trainingRunId,
            TrackingAvailable: true,
            MlflowRunUuid:   run.RunUuid,
            RunName:          run.RunName,
            Status:           run.Status,
            StartTime:        run.StartTime,
            EndTime:          run.EndTime,
            ArtifactUri:      run.ArtifactUri,
            Params:           parameters,
            RewardMetrics:    MlflowRewardTrackingDashboardDto.From(latestMetrics, metricHistory),
            LatestMetrics:    latestMetrics,
            MetricHistory:    metricHistory,
            Experiment:       experiment,
            Tags:             tags,
            Registry:         registry,
            PpoInternals:     ppoInternals,
            EvalMetrics:      evalMetrics.Count > 0 ? evalMetrics : null);
    }

    // ── Metric helper (mirrors MlflowRewardTrackingDashboardDto.Metric) ────────

    private static MlflowTrackedMetricDto Metric(
        IReadOnlyDictionary<string, double?> latestMetrics,
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory,
        string label,
        params string[] candidateKeys)
    {
        var key = FindMetricKey(latestMetrics, metricHistory, candidateKeys);
        var history = key is not null ? FindMetricHistory(metricHistory, key) : [];
        var latestValue = key is not null && TryGetLatestMetricValue(latestMetrics, key, out var value)
            ? value
            : history.LastOrDefault()?.Value;

        return new MlflowTrackedMetricDto(
            Key: key,
            Label: label,
            WhatItChecks: string.Empty,
            LatestValue: latestValue,
            History: history);
    }

    private static bool TryGetLatestMetricValue(
        IReadOnlyDictionary<string, double?> latestMetrics,
        string metricKey,
        out double? value)
    {
        var key = latestMetrics.Keys.FirstOrDefault(
            candidate => candidate.Equals(metricKey, StringComparison.OrdinalIgnoreCase));
        if (key is not null)
            return latestMetrics.TryGetValue(key, out value);

        value = null;
        return false;
    }

    private static IReadOnlyList<MlflowMetricPointDto> FindMetricHistory(
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory,
        string metricKey)
    {
        var key = metricHistory.Keys.FirstOrDefault(
            candidate => candidate.Equals(metricKey, StringComparison.OrdinalIgnoreCase));

        return key is not null && metricHistory.TryGetValue(key, out var points) ? points : [];
    }

    private static string? FindMetricKey(
        IReadOnlyDictionary<string, double?> latestMetrics,
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory,
        params string[] candidateKeys)
    {
        foreach (var candidateKey in candidateKeys)
        {
            var latestKey = latestMetrics.Keys.FirstOrDefault(
                key => key.Equals(candidateKey, StringComparison.OrdinalIgnoreCase));
            if (latestKey is not null)
                return latestKey;

            var historyKey = metricHistory.Keys.FirstOrDefault(
                key => key.Equals(candidateKey, StringComparison.OrdinalIgnoreCase));
            if (historyKey is not null)
                return historyKey;
        }

        return null;
    }

    // ── Raw Npgsql helpers (used by GetTrackingSummariesAsync only) ────────────

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
                "disable"     => SslMode.Disable,
                "allow"       => SslMode.Allow,
                "prefer"      => SslMode.Prefer,
                "verify-ca"   => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                _             => SslMode.Require
            };
        }

        return SslMode.Require;
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

    // ── Shared utilities ───────────────────────────────────────────────────────

    private static DateTimeOffset FromUnixMilliseconds(long timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

    private sealed record MlflowRunRow(
        long TrainingRunId,
        string RunUuid,
        string? RunName,
        string? Status,
        DateTimeOffset? StartTime,
        DateTimeOffset? EndTime,
        string? ArtifactUri,
        int? ExperimentId = null);
}
