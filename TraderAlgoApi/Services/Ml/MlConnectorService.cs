using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Services.Ml;

public sealed class MlConnectorService(
    IHttpClientFactory httpClientFactory,
    ILogger<MlConnectorService> logger) : IMlConnectorService
{
    private const string HttpClientName = "MlPolicy";

    // Omit null hyperparameters when forwarding to Python so its non-nullable fields fall back to
    // their own defaults instead of receiving an invalid null.
    private static readonly JsonSerializerOptions TrainJsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task<MlDecideResponse> DecideAsync(
        MlDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        logger.LogInformation(
            "Sending decide request to ML service: symbol={Symbol}, interval={Interval}, model={ModelId}, position={Position}",
            request.Symbol, request.Interval, request.ModelId, request.Position);

        using var response = await httpClient.PostAsJsonAsync("/decide", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "ML service returned {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<MlDecideResponse>(cancellationToken);
        return result!;
    }

    public async Task<MlTrainResponse> TrainAsync(
        MlTrainRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        logger.LogInformation(
            "Sending train request to ML service: symbol={Symbol}, interval={Interval}, model={ModelId}, from={FromDate}, to={ToDate}",
            request.Symbol, request.Interval, request.ModelId, request.FromDate, request.ToDate);

        using var response = await httpClient.PostAsJsonAsync("/train", request, TrainJsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "ML service returned {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<MlTrainResponse>(cancellationToken);
        return result!;
    }

    public async Task<MlTrainingDecisionsResponse?> GetTrainingDecisionsAsync(
        long trainingRunId,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var response = await httpClient.GetAsync(
            $"/training-runs/{trainingRunId}/decisions",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("ML service has no decision log for training run {RunId}", trainingRunId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "ML service returned {StatusCode} fetching decisions for run {RunId}: {Body}",
                (int)response.StatusCode, trainingRunId, body);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<MlTrainingDecisionsResponse>(cancellationToken);
    }

    public async Task DeleteTrainingDecisionsAsync(
        long trainingRunId,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        // Best-effort cleanup: a failure here must not block deleting the DB record.
        try
        {
            using var response = await httpClient.DeleteAsync(
                $"/training-runs/{trainingRunId}/decisions", cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                logger.LogWarning(
                    "ML service returned {StatusCode} deleting decisions for run {RunId}",
                    (int)response.StatusCode, trainingRunId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete decision log for run {RunId} on the ML service", trainingRunId);
        }
    }
}
