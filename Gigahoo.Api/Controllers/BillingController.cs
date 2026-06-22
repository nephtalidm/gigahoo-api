using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillingController(
    GigahooDbContext db,
    IStripeService stripe) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpGet("summary")]
    public async Task<ActionResult<BillingSummaryResponse>> GetSummary()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts
            .Include(a => a.Plan)
            .FirstAsync(a => a.Id == accountId);

        var remaining = Math.Max(0, account.Plan.IncludedMinutes - account.MinutesUsed);
        var usagePercent = account.Plan.IncludedMinutes > 0
            ? Math.Round((double)account.MinutesUsed / account.Plan.IncludedMinutes * 100, 1)
            : 0;

        var billingPeriod = account.BillingPeriodStart.HasValue && account.BillingPeriodEnd.HasValue
            ? $"{account.BillingPeriodStart:MMM d} - {account.BillingPeriodEnd:MMM d}"
            : "";

        return Ok(new BillingSummaryResponse(
            account.Plan.Name,
            account.Plan.IncludedMinutes,
            account.MinutesUsed,
            remaining,
            billingPeriod,
            usagePercent
        ));
    }

    [HttpGet("plans")]
    public async Task<ActionResult<List<PlanResponse>>> GetPlans()
    {
        var plans = await db.Plans.Where(p => p.IsActive).ToListAsync();

        var result = plans.Select(p => new PlanResponse(
            p.Id,
            p.Name,
            p.PriceMonthly,
            p.IncludedMinutes,
            p.HasOptionalFeatures,
            GetPlanFeatures(p)
        )).ToList();

        return Ok(result);
    }

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.Id == accountId);
        var newPlan = await db.Plans.FindAsync(request.PlanId);

        if (newPlan is null) return NotFound(new { error = "Plan not found" });

        account.PlanId = request.PlanId;
        account.UpdatedAt = DateTime.UtcNow;

        // Handle Stripe subscription change if applicable
        if (account.StripeSubscriptionId is not null && newPlan.PriceMonthly > 0)
        {
            // In production: update Stripe subscription via StripeService
        }
        else if (account.StripeSubscriptionId is not null && newPlan.PriceMonthly == 0)
        {
            await stripe.CancelSubscriptionAsync(account.StripeSubscriptionId);
            account.StripeSubscriptionId = null;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Plan updated", plan = newPlan.Name });
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<List<InvoiceResponse>>> GetInvoices()
    {
        var accountId = GetAccountId();
        var invoices = await db.Invoices
            .Where(i => i.AccountId == accountId)
            .OrderByDescending(i => i.DateUtc)
            .Select(i => new InvoiceResponse(
                i.Id, i.InvoiceNumber, i.DateUtc, i.Amount, i.Currency, i.Status, i.PdfUrl
            ))
            .ToListAsync();

        return Ok(invoices);
    }

    [HttpGet("payment-method")]
    public async Task<ActionResult<PaymentMethodResponse>> GetPaymentMethod()
    {
        var accountId = GetAccountId();
        var pm = await db.PaymentMethods
            .Where(p => p.AccountId == accountId && p.IsDefault)
            .FirstOrDefaultAsync();

        if (pm is null) return NotFound();

        return Ok(new PaymentMethodResponse(pm.Id, pm.Brand, pm.Last4, pm.ExpMonth, pm.ExpYear, pm.IsDefault));
    }

    [HttpPost("portal")]
    public async Task<IActionResult> OpenPortal()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstAsync(a => a.Id == accountId);

        if (account.StripeCustomerId is null)
            return BadRequest(new { error = "No Stripe customer found" });

        var returnUrl = $"{Request.Scheme}://{Request.Host}/dashboard/billing";
        var url = await stripe.CreateBillingPortalSessionAsync(account.StripeCustomerId, returnUrl);

        return Ok(new { url });
    }

    private static List<string> GetPlanFeatures(Entities.Plan plan) => plan.Name switch
    {
        "Free" => ["24/7 AI receptionist", "Multilingual support", "Customer intake", "Call summaries", "25 included minutes"],
        "Starter" => ["24/7 AI receptionist", "Multilingual support", "Customer intake", "Call summaries", "250 included minutes"],
        "Business" => ["24/7 AI receptionist", "Multilingual support", "Customer intake", "Call summaries", "1,000 included minutes", "Answers questions about services"],
        _ => []
    };
}
