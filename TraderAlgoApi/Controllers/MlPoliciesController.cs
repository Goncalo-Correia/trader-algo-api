using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Ml;
using TraderAlgoApi.Models;

namespace TraderAlgoApi.Controllers;

[ApiController]
[Route("api/ml")]
public sealed class MlPoliciesController(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : ControllerBase
{
    // ── Policies ────────────────────────────────────────────────────────────────────
    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<MlPolicyResponse>>> GetPolicies(CancellationToken cancellationToken)
    {
        var rows = await dbContext.MlPolicies
            .AsNoTracking()
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { Policy = p, RunCount = p.TrainingRuns.Count })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(r => ToDto(r.Policy, r.RunCount)).ToList());
    }

    [HttpGet("policies/{id:long}")]
    public async Task<ActionResult<MlPolicyResponse>> GetPolicy(long id, CancellationToken cancellationToken)
    {
        var row = await dbContext.MlPolicies
            .AsNoTracking()
            .Include(p => p.Symbol)
            .Include(p => p.Interval)
            .Where(p => p.Id == id)
            .Select(p => new { Policy = p, RunCount = p.TrainingRuns.Count })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound($"Policy {id} not found.") : Ok(ToDto(row.Policy, row.RunCount));
    }

    [HttpPost("policies")]
    public async Task<ActionResult<MlPolicyResponse>> CreatePolicy(
        [FromBody] MlPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveReferencesAsync(request, cancellationToken);
        if (resolved.Error is not null)
            return NotFound(resolved.Error);

        var policy = new MlPolicy
        {
            SymbolId   = resolved.SymbolId,
            IntervalId = resolved.IntervalId,
            CreatedAt  = timeProvider.GetUtcNow()
        };
        Apply(policy, request);

        dbContext.MlPolicies.Add(policy);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetPolicy(policy.Id, cancellationToken);
    }

    [HttpPut("policies/{id:long}")]
    public async Task<ActionResult<MlPolicyResponse>> UpdatePolicy(
        long id,
        [FromBody] MlPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.MlPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null)
            return NotFound($"Policy {id} not found.");

        var resolved = await ResolveReferencesAsync(request, cancellationToken);
        if (resolved.Error is not null)
            return NotFound(resolved.Error);

        policy.SymbolId   = resolved.SymbolId;
        policy.IntervalId = resolved.IntervalId;
        Apply(policy, request);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetPolicy(policy.Id, cancellationToken);
    }

    [HttpDelete("policies/{id:long}")]
    public async Task<IActionResult> DeletePolicy(long id, CancellationToken cancellationToken)
    {
        var policy = await dbContext.MlPolicies
            .Include(p => p.TrainingRuns)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null)
            return NotFound($"Policy {id} not found.");

        if (policy.TrainingRuns.Count > 0)
            return Conflict($"Policy {id} has {policy.TrainingRuns.Count} training run(s); delete those first.");

        dbContext.MlPolicies.Remove(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // -------------------------------------------------------------------------

    private async Task<(int SymbolId, int IntervalId, string? Error)> ResolveReferencesAsync(
        MlPolicyRequest request, CancellationToken cancellationToken)
    {
        var symbolId = await dbContext.Symbols
            .Where(s => s.Code == request.Symbol).Select(s => (int?)s.Id).FirstOrDefaultAsync(cancellationToken);
        if (symbolId is null)
            return (0, 0, $"Symbol '{request.Symbol}' not found.");

        var intervalId = await dbContext.Intervals
            .Where(i => i.Code == request.Interval).Select(i => (int?)i.Id).FirstOrDefaultAsync(cancellationToken);
        if (intervalId is null)
            return (0, 0, $"Interval '{request.Interval}' not found.");

        return (symbolId.Value, intervalId.Value, null);
    }

    private static void Apply(MlPolicy policy, MlPolicyRequest r)
    {
        policy.TotalTimesteps      = r.TotalTimesteps;
        policy.InitialBalance      = r.InitialBalance;
        policy.Quantity            = r.Quantity;
        policy.Breakeven           = r.Breakeven;
        policy.BreakevenStop       = r.BreakevenStop;
        policy.Fee                 = r.Fee;
        policy.Slippage            = r.Slippage;
        policy.DailyProfit         = r.DailyProfit;
        policy.DailyDrawdownLimit  = r.DailyDrawdownLimit;
        policy.MaxCandlesPerTrade  = r.MaxCandlesPerTrade;
        policy.RiskPerTrade        = r.RiskPerTrade;
    }

    private static MlPolicyResponse ToDto(MlPolicy p, int trainingRunCount) =>
        new(
            Id:                  p.Id,
            SymbolId:            p.SymbolId,
            SymbolCode:          p.Symbol.Code,
            IntervalId:          p.IntervalId,
            IntervalCode:        p.Interval.Code,
            TotalTimesteps:      p.TotalTimesteps,
            InitialBalance:      p.InitialBalance,
            Quantity:            p.Quantity,
            Breakeven:           p.Breakeven,
            BreakevenStop:       p.BreakevenStop,
            Fee:                 p.Fee,
            Slippage:            p.Slippage,
            DailyProfit:         p.DailyProfit,
            DailyDrawdownLimit:  p.DailyDrawdownLimit,
            MaxCandlesPerTrade:  p.MaxCandlesPerTrade,
            RiskPerTrade:        p.RiskPerTrade,
            CreatedAt:           p.CreatedAt.ToUnixTimeMilliseconds(),
            TrainingRunCount:    trainingRunCount);
}
