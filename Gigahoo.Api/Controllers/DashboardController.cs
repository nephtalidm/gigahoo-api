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
        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.AccountId == accountId);

        var conversationsQuery = db.Conversations.Where(c => c.AccountId == accountId);

        var conversationsAnswered = await conversationsQuery.CountAsync(c =>
            c.ConversationStatusId == (byte)Entities.ConversationStatusId.Answered ||
            c.ConversationStatusId == (byte)Entities.ConversationStatusId.Completed);
        var avgDuration = await conversationsQuery
            .Where(c => c.DurationSeconds > 0)
            .AverageAsync(c => (double?)c.DurationSeconds) ?? 0;

        var recentConversations = await db.Conversations
            .Include(c => c.Language)
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.DateTimeUtc)
            .Take(4)
            .Select(c => new ConversationResponse(
                c.ConversationId,
                c.CallerName,
                c.CallerPhoneNumber,
                c.DateTimeUtc,
                c.DurationSeconds,
                c.Language != null ? c.Language.Name : "English",
                c.Summary,
                c.Address,
                c.IsEmergency,
                c.ConversationStatus!.Name
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
            conversationsAnswered,
            avgDuration,
            recentConversations
        ));
    }
}
