using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("api")]
public class DashboardController(GigahooDbContext db) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewResponse>> GetOverview()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.Id == accountId);

        var callsQuery = db.Calls.Where(c => c.AccountId == accountId);

        var callsAnswered = await callsQuery.CountAsync(c => c.Status == "Answered" || c.Status == "Completed");
        var avgDuration = await callsQuery
            .Where(c => c.DurationSeconds > 0)
            .AverageAsync(c => (double?)c.DurationSeconds) ?? 0;

        var recentCalls = await db.Calls
            .Include(c => c.Language)
            .Include(c => c.CollectedInfo)
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.DateTimeUtc)
            .Take(4)
            .Select(c => new CallResponse(
                c.Id,
                c.CallerName,
                c.CallerPhone,
                c.DateTimeUtc,
                c.DurationSeconds,
                c.Language != null ? c.Language.Name : "English",
                c.Summary,
                c.Status,
                c.CollectedInfo.Select(ci => new CollectedInfoDto(ci.Label, ci.Value)).ToList()
            ))
            .ToListAsync();

        var remaining = Math.Max(0, account.Plan.IncludedMinutes - account.MinutesUsed);

        var billingPeriod = account.BillingPeriodStart.HasValue && account.BillingPeriodEnd.HasValue
            ? $"{account.BillingPeriodStart:MMM d} - {account.BillingPeriodEnd:MMM d}"
            : "";

        return Ok(new DashboardOverviewResponse(
            account.Plan.Name,
            account.Plan.IncludedMinutes,
            account.MinutesUsed,
            remaining,
            billingPeriod,
            callsAnswered,
            avgDuration,
            recentCalls
        ));
    }
}
