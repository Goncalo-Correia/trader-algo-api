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
}
