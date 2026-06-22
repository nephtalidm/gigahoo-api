using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(GigahooDbContext db) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewResponse>> GetOverview()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.Id == accountId);

        var calls = await db.Calls
            .Include(c => c.Language)
            .Include(c => c.CollectedInfo)
            .Where(c => c.AccountId == accountId)
            .ToListAsync();

        var callsAnswered = calls.Count(c => c.Status is "Answered" or "Completed");
        var callsWithDuration = calls.Where(c => c.DurationSeconds > 0).ToList();
        var avgDuration = callsWithDuration.Count > 0
            ? callsWithDuration.Average(c => c.DurationSeconds)
            : 0;

        var remaining = Math.Max(0, account.Plan.IncludedMinutes - account.MinutesUsed);

        var billingPeriod = account.BillingPeriodStart.HasValue && account.BillingPeriodEnd.HasValue
            ? $"{account.BillingPeriodStart:MMM d} - {account.BillingPeriodEnd:MMM d}"
            : "";

        var recentCalls = calls
            .OrderByDescending(c => c.DateTimeUtc)
            .Take(4)
            .Select(c => new CallResponse(
                c.Id,
                c.CallerName,
                c.CallerPhone,
                c.DateTimeUtc,
                c.DurationSeconds,
                c.Language?.Name ?? "English",
                c.Summary,
                c.Status,
                c.CollectedInfo.Select(ci => new CollectedInfoDto(ci.Label, ci.Value)).ToList()
            ))
            .ToList();

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
