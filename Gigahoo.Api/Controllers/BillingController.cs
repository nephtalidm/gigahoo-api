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
    IConfiguration config,
    ITwilioService twilio,
    Gigahoo.Api.Services.Providers.ITelephonyProvider telephony,
    ILogger<BillingController> logger) : ControllerBase
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

    // Public, per-currency plan prices for the marketing site's pricing section.
    // Anonymous (like /api/countries/supported) and rendered server-side so the
    // homepage shows the visitor's currency with no client-fetch flicker.
    //
    // Currency resolution is fully data-driven: Country.Currency for the given
    // code (ANY country, including coming-soon markets), falling back to the US
    // row's currency, then "USD". Each active plan's amount comes from PlanPrice
    // for that currency (0 for free plans), falling back to Plan.PriceMonthly.
    [HttpGet("public-prices")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicPricesResponse>> GetPublicPrices([FromQuery] string? code)
    {
        var match = await db.Countries.FirstOrDefaultAsync(c => c.Code == (code ?? ""));
        var currency = (match?.Currency
            ?? (await db.Countries.FirstOrDefaultAsync(c => c.Code == "US"))?.Currency)
            ?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(currency)) currency = "USD";

        var plans = await db.Plans.Where(p => p.IsActive).ToListAsync();
        var prices = await db.PlanPrices
            .Where(pp => pp.Currency == currency && pp.IsActive)
            .ToListAsync();

        var result = plans.Select(p =>
        {
            decimal amount = p.PriceMonthly == 0
                ? 0m
                : prices.FirstOrDefault(pp => pp.PlanId == p.Id)?.Amount ?? p.PriceMonthly;
            return new PublicPlanPrice(p.Name, amount);
        }).ToList();

        return Ok(new PublicPricesResponse(currency, result));
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

        // Billing currency is fully data-driven: read it from the Country table
        // (no hardcoded country->currency mapping or default currency in code).
        var country = account.CountryCodeId is short cid ? await db.Countries.FindAsync(cid) : null;
        country ??= await db.Countries.FirstOrDefaultAsync(c => c.Code == account.PhoneCountryCode);
        var currency = country?.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(currency))
            return BadRequest(new { error = "Could not determine a billing currency for this account's country" });

        // Defense-in-depth: Gigahoo only serves countries flagged IsSupported in the
        // Country table. Validate the resolved billing country against that flag.
        var billingCountryCode = (country?.Code ?? account.PhoneCountryCode)?.Trim();
        var billingCountrySupported = await db.Countries.AnyAsync(c => c.Code == billingCountryCode && c.IsSupported);
        if (!billingCountrySupported)
            return BadRequest(new { error = "Gigahoo is currently available only in the US and Canada." });

        // Price comes from the PlanPrice table for that currency. If it isn't set up,
        // fail explicitly rather than defaulting to a currency.
        var planPrice = await db.PlanPrices.FirstOrDefaultAsync(pp => pp.PlanId == plan.Id && pp.Currency == currency && pp.IsActive);
        if (planPrice is null || string.IsNullOrEmpty(planPrice.StripePriceId))
            return BadRequest(new { error = $"No Stripe price configured for plan '{plan.Name}' in {currency}" });
        var priceId = planPrice.StripePriceId;

        // Reserve-then-charge: secure the phone number BEFORE creating the Checkout
        // session so we never take payment for a number we can't deliver. The
        // assignment email/SMS are sent later (on the first paid invoice), not here.
        if (string.IsNullOrEmpty(account.PhoneNumberSid))
        {
            var numberCountryCode = country?.Code ?? account.PhoneCountryCode ?? "US";
            try
            {
                // Pool-first, then purchase a fresh number if the pool is empty.
                var phoneNumber = await twilio.GetAvailableNumberAsync(numberCountryCode);
                if (phoneNumber == null)
                {
                    var purchased = await twilio.PurchasePhoneNumberAsync(numberCountryCode);
                    if (purchased is not null)
                    {
                        phoneNumber = new Entities.PhoneNumber
                        {
                            Sid = purchased.Sid,
                            Number = purchased.PhoneNumber,
                            CountryCode = numberCountryCode,
                            Provider = telephony.ProviderName,
                            Status = Entities.PhoneNumberStatus.Available,
                            MonthlyCost = 1.15m,
                            PurchasedAt = DateTime.UtcNow
                        };
                        db.PhoneNumbers.Add(phoneNumber);
                        await db.SaveChangesAsync();
                        logger.LogInformation("Purchased new phone number {Number} and added to pool", purchased.PhoneNumber);
                    }
                }

                if (phoneNumber == null)
                {
                    logger.LogWarning("Could not reserve a phone number for account {Account} before checkout", account.Id);
                    return BadRequest(new { error = "We couldn't reserve a phone number for your area right now — you have not been charged. Please try again shortly." });
                }

                // Assign the number to the account so PhoneNumberSid is set, then
                // point its voice webhook at the agent (same as the webhook handler).
                await twilio.AssignNumberToAccountAsync(phoneNumber, account.Id);
                account.PhoneNumberSid = phoneNumber.Sid;
                account.TelephonyProvider = phoneNumber.Provider;
                account.ForwardingPhone = phoneNumber.Number;

                var webhookUrl = $"{config["VoiceAgent:PublicUrl"]}/twilio/voice?accountId={account.Id}";
                await twilio.ConfigureWebhookAsync(phoneNumber.Sid, webhookUrl);

                account.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                logger.LogInformation("Reserved phone number {Number} for account {Account} before checkout", phoneNumber.Number, account.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reserving phone number for account {Account} before checkout", account.Id);
                return BadRequest(new { error = "We couldn't reserve a phone number for your area right now — you have not been charged. Please try again shortly." });
            }
        }

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
public record PublicPlanPrice(string Slug, decimal Amount);
public record PublicPricesResponse(string Currency, List<PublicPlanPrice> Plans);
