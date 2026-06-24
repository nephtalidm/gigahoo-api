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
[EnableRateLimiting("api")]
public class BillingController(
    GigahooDbContext db,
    IStripeService stripe,
    IConfiguration config) : ControllerBase
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
            ? (decimal)Math.Round((double)account.MinutesUsed / account.Plan.IncludedMinutes * 100, 1)
            : 0m;

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

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> CreateCheckout([FromBody] CheckoutRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.Id == accountId);
        var plan = await db.Plans.FindAsync(request.PlanId);

        if (plan is null) return NotFound(new { error = "Plan not found" });
        if (plan.PriceMonthly == 0) return BadRequest(new { error = "Free plans don't need checkout" });

        // Create Stripe customer if not exists
        if (account.StripeCustomerId is null)
        {
            var customerId = await stripe.CreateCustomerAsync(account.Email!, account.BusinessName);
            account.StripeCustomerId = customerId;
            await db.SaveChangesAsync();
        }

        var priceId = config[$"Stripe:PriceIds:{request.PlanId}"];
        if (string.IsNullOrEmpty(priceId)) return BadRequest(new { error = "No Stripe price configured for this plan" });

        var successUrl = $"{Request.Scheme}://{Request.Host}/dashboard/billing?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{Request.Scheme}://{Request.Host}/dashboard/billing";

        var url = await stripe.CreateCheckoutSessionAsync(account.StripeCustomerId, priceId, successUrl, cancelUrl);

        return Ok(new CheckoutResponse(url));
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

public record CheckoutRequest(byte PlanId);
public record CheckoutResponse(string Url);
