namespace TraderAlgoApi.Services.Charts;

public interface ILiveChartDataService
{
    Task StreamCandlesAsync(
        HttpContext context,
        string? symbol = null,
        string? interval = null,
        CancellationToken cancellationToken = default);
}
