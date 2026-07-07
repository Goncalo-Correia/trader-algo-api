using System.Net.Http.Json;
using TraderAlgoApi.Dtos.Ml;

namespace TraderAlgoApi.Services.Ml;

public sealed class MlConnectorService(
    IHttpClientFactory httpClientFactory,
    ILogger<MlConnectorService> logger) : IMlConnectorService
{
    private const string HttpClientName = "MlPolicy";

    public async Task<MlDecideResponse> DecideAsync(
        MlDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        logger.LogInformation(
            "Sending decide request to ML service: policy={PolicyId}, symbol={Symbol}, interval={Interval}, model={ModelId}, position={Position}",
            request.MlPolicyId, request.Symbol, request.Interval, request.ModelId, request.Position);

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
            "Sending train request to ML service: policy={PolicyId}, run={TrainingRunId}, symbol={Symbol}, interval={Interval}, model={ModelId}, from={FromDate}, to={ToDate}",
            request.MlPolicyId, request.TrainingRunId, request.Symbol, request.Interval, request.ModelId, request.FromDate, request.ToDate);

        using var response = await httpClient.PostAsJsonAsync("/train", request, cancellationToken);

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

    public async Task<IReadOnlyList<MlModelInfoResponse>> GetModelsAsync(
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var response = await httpClient.GetAsync("/models", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "ML service returned {StatusCode} fetching models: {Body}",
                (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        // The ML service wraps the list in { "models": [ ... ] }.
        var result = await response.Content.ReadFromJsonAsync<MlModelsEnvelope>(cancellationToken);
        return result?.Models ?? [];
    }

}
