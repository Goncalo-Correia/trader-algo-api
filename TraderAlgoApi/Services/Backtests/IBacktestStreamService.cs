namespace TraderAlgoApi.Services.Backtests;

public interface IBacktestStreamService
{
    Task StreamAsync(HttpContext context, long backtestId, CancellationToken cancellationToken = default);
}
