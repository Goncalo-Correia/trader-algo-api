using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Services.DataCollector;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/data-collector")]
public sealed class DataCollectorController(
    ApplicationDbContext dbContext,
    IDataCollectorService dataCollectorService) : ControllerBase
{
    private static readonly DateTimeOffset DataStartDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [HttpPost("{symbol}/{interval}")]
    public async Task<ActionResult<DataCollectionResult>> CollectKlines(
        string symbol,
        string interval,
        CancellationToken cancellationToken)
    {
        var result = await dataCollectorService.CollectKlinesAsync(symbol, interval, DataStartDate, cancellationToken);

        return Ok(result);
    }

    [HttpPost("full-sync")]
    public async Task<ActionResult<IReadOnlyList<DataCollectionResult>>> FullSync(CancellationToken cancellationToken)
    {
        var symbols = await dbContext.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        var intervals = await dbContext.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Duration)
            .ToListAsync(cancellationToken);

        var results = new List<DataCollectionResult>();

        foreach (var symbol in symbols)
        {
            foreach (var interval in intervals)
            {
                var result = await dataCollectorService.CollectKlinesAsync(
                    symbol.Code, interval.Code, DataStartDate, cancellationToken);

                results.Add(result);
            }
        }

        return Ok(results);
    }
}
