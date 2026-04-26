using System.Net.Http.Json;
using TraderAlgoApi.Dtos.Kronos;

namespace TraderAlgoApi.Services.Kronos;

public sealed class KronosConnectorService(
    IHttpClientFactory httpClientFactory,
    ILogger<KronosConnectorService> logger) : IKronosConnectorService
{
    private const string HttpClientName = "Kronos";

    public async Task<KronosPredictResponse> PredictAsync(
        KronosPredictRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        logger.LogInformation(
            "Sending predict request to Kronos: symbol={Symbol}, model={ModelId}, candles={CandleCount}, predLen={PredLen}",
            request.Symbol, request.ModelId, request.Candles.Count, request.PredLen);

        using var response = await httpClient.PostAsJsonAsync("/predict", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Kronos returned {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<KronosPredictResponse>(cancellationToken);
        return result!;
    }
}
