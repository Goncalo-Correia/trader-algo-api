using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Models;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/intervals")]
public sealed class IntervalsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Interval>>> GetActive(CancellationToken cancellationToken)
    {
        var intervals = await dbContext.Intervals
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Duration)
            .ToListAsync(cancellationToken);

        return Ok(intervals);
    }
}
