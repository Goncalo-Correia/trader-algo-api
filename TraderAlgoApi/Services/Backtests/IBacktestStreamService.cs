namespace TraderAlgoApi.Services.Backtests;

public interface IBacktestStreamService
{
    Task StreamAsync(HttpContext context, long backtestId, bool delay = false, CancellationToken cancellationToken = default);
}
