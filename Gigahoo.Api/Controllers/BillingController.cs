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
    IPaymentProviderRegistry payments,
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
            .FirstAsync(a => a.AccountId == accountId);

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
        var plans = await db.Plans.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).ToListAsync();

        var result = plans.Select(p => new PlanResponse(
            p.PlanId,
            p.Name,
            p.PriceMonthly,
            p.IncludedMinutes,
            p.HasOptionalFeatures,
            p.DisplayOrder,
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

        // Restrict to the default payment provider's prices so there's exactly one
        // row per (plan, currency) even if other providers later add their own prices.
        var defaultProvider = await db.Providers.FirstOrDefaultAsync(
            p => p.Code == payments.Default.Name && p.ProviderTypeId == (byte)Entities.ProviderTypeId.Payment);
        var prices = defaultProvider is null
            ? new List<Entities.PlanPrice>()
            : await db.PlanPrices
                .Where(pp => pp.Currency == currency && pp.IsActive && pp.ProviderId == defaultProvider.ProviderId)
                .ToListAsync();

        var result = plans.Select(p =>
        {
            decimal amount = p.PriceMonthly == 0
                ? 0m
                : prices.FirstOrDefault(pp => pp.PlanId == p.PlanId)?.Amount ?? p.PriceMonthly;
            return new PublicPlanPrice(p.Name, amount);
        }).ToList();

        return Ok(new PublicPricesResponse(currency, result));
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> CreateCheckout([FromBody] CheckoutRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.AccountId == accountId);
        var plan = await db.Plans.FindAsync(request.PlanId);

        if (plan is null) return NotFound(new { error = "Plan not found" });
        if (plan.PriceMonthly == 0) return BadRequest(new { error = "Free plans don't need checkout" });

        // Checkout uses the default provider for new payments.
        var provider = payments.Default;
        var providerRow = await db.Providers.FirstOrDefaultAsync(
            p => p.Code == provider.Name && p.ProviderTypeId == (byte)Entities.ProviderTypeId.Payment);
        if (providerRow is null)
            return BadRequest(new { error = $"Payment provider '{provider.Name}' is not configured." });

        // Get-or-create the provider customer id (creates if missing).
        var customerId = await provider.EnsureCustomerAsync(account);

        // Billing currency is fully data-driven: read it from the Country table
        // (no hardcoded country->currency mapping or default currency in code).
        var country = account.CountryCodeId is short cid ? await db.Countries.FindAsync(cid) : null;
        var currency = country?.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(currency))
            return BadRequest(new { error = "Could not determine a billing currency for this account's country" });

        // Defense-in-depth: Gigahoo only serves countries flagged IsSupported in the
        // Country table. Validate the resolved billing country against that flag.
        var billingCountryCode = country?.Code?.Trim();
        var billingCountrySupported = await db.Countries.AnyAsync(c => c.Code == billingCountryCode && c.IsSupported);
        if (!billingCountrySupported)
            return BadRequest(new { error = "Gigahoo is currently available only in the US and Canada." });

        // Price comes from the PlanPrice table for that currency. If it isn't set up,
        // fail explicitly rather than defaulting to a currency.
        var planPrice = await db.PlanPrices.FirstOrDefaultAsync(pp => pp.PlanId == plan.PlanId && pp.Currency == currency && pp.ProviderId == providerRow.ProviderId && pp.IsActive);
        if (planPrice is null || string.IsNullOrEmpty(planPrice.ProviderPriceId))
            return BadRequest(new { error = $"No {provider.Name} price configured for plan '{plan.Name}' in {currency}" });
        var priceId = planPrice.ProviderPriceId;

        // Reserve-then-charge: secure the phone number BEFORE creating the Checkout
        // session so we never take payment for a number we can't deliver. The
        // assignment email/SMS are sent later (on the first paid invoice), not here.
        if (account.AssignedPhoneNumberId is null)
        {
            var numberCountryCode = country?.Code ?? "US";
            try
            {
                // Pool-first, then purchase a fresh number if the pool is empty.
                var phoneNumber = await twilio.GetAvailableNumberAsync(numberCountryCode);
                if (phoneNumber == null)
                {
                    var purchased = await twilio.PurchasePhoneNumberAsync(numberCountryCode);
                    if (purchased is not null)
                    {
                        phoneNumber = await twilio.AddPurchasedNumberToPoolAsync(purchased, numberCountryCode);
                        logger.LogInformation("Purchased new phone number {Number} and added to pool", purchased.PhoneNumber);
                    }
                }

                if (phoneNumber == null)
                {
                    logger.LogWarning("Could not reserve a phone number for account {Account} before checkout", account.AccountId);
                    return BadRequest(new { error = "We couldn't reserve a phone number for your area right now — you have not been charged. Please try again shortly." });
                }

                // Assign the number to the account, then point its voice webhook at the
                // agent (same as the webhook handler).
                await twilio.AssignNumberToAccountAsync(phoneNumber, account.AccountId);
                account.AssignedPhoneNumberId = phoneNumber.PhoneNumberId;

                var webhookUrl = $"{config["VoiceAgent:PublicUrl"]}/twilio/voice?accountId={account.AccountId}";
                await twilio.ConfigureWebhookAsync(phoneNumber.Sid, webhookUrl);

                account.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                logger.LogInformation("Reserved phone number {Number} for account {Account} before checkout", phoneNumber.Number, account.AccountId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reserving phone number for account {Account} before checkout", account.AccountId);
                return BadRequest(new { error = "We couldn't reserve a phone number for your area right now — you have not been charged. Please try again shortly." });
            }
        }

        var successUrl = $"{Request.Scheme}://{Request.Host}/dashboard/plan?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{Request.Scheme}://{Request.Host}/dashboard/plan";

        var url = await stripe.CreateCheckoutSessionAsync(customerId, priceId, successUrl, cancelUrl);

        return Ok(new CheckoutResponse(url));
    }

    /// <summary>
    /// EMBEDDED plan purchase/upgrade — the user never leaves the dashboard. Same validation and
    /// reserve-then-charge as checkout, then: an existing subscription is re-priced in place
    /// (prorated); otherwise a subscription is created charging the saved default card directly.
    /// With no saved card (or when the charge needs 3DS), the PaymentIntent client secret comes
    /// back and the in-app card form completes it.
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] CheckoutRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.AccountId == accountId);
        var plan = await db.Plans.FindAsync(request.PlanId);

        if (plan is null) return NotFound(new { error = "Plan not found" });
        if (plan.PriceMonthly == 0) return BadRequest(new { error = "Free plans don't need checkout" });

        var provider = payments.Default;
        var providerRow = await db.Providers.FirstOrDefaultAsync(
            p => p.Code == provider.Name && p.ProviderTypeId == (byte)Entities.ProviderTypeId.Payment);
        if (providerRow is null)
            return BadRequest(new { error = $"Payment provider '{provider.Name}' is not configured." });

        var customerId = await provider.EnsureCustomerAsync(account);

        var country = account.CountryCodeId is short cid ? await db.Countries.FindAsync(cid) : null;
        var currency = country?.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(currency))
            return BadRequest(new { error = "Could not determine a billing currency for this account's country" });
        var billingCountrySupported = await db.Countries.AnyAsync(c => c.Code == country!.Code && c.IsSupported);
        if (!billingCountrySupported)
            return BadRequest(new { error = "Gigahoo is currently available only in the US and Canada." });

        var planPrice = await db.PlanPrices.FirstOrDefaultAsync(pp => pp.PlanId == plan.PlanId && pp.Currency == currency && pp.ProviderId == providerRow.ProviderId && pp.IsActive);
        if (planPrice is null || string.IsNullOrEmpty(planPrice.ProviderPriceId))
            return BadRequest(new { error = $"No {provider.Name} price configured for plan '{plan.Name}' in {currency}" });
        var priceId = planPrice.ProviderPriceId;

        // Reserve-then-charge: same rule as checkout — never take payment for a number we
        // can't deliver.
        if (account.AssignedPhoneNumberId is null)
        {
            var numberCountryCode = country?.Code ?? "US";
            try
            {
                var phoneNumber = await twilio.GetAvailableNumberAsync(numberCountryCode);
                if (phoneNumber == null)
                {
                    var purchased = await twilio.PurchasePhoneNumberAsync(numberCountryCode);
                    if (purchased is not null)
                        phoneNumber = await twilio.AddPurchasedNumberToPoolAsync(purchased, numberCountryCode);
                }
                if (phoneNumber == null)
                    return BadRequest(new { error = "We couldn't reserve a phone number for your area right now — you have not been charged. Please try again shortly." });

                await twilio.AssignNumberToAccountAsync(phoneNumber, account.AccountId);
                account.AssignedPhoneNumberId = phoneNumber.PhoneNumberId;
                await twilio.ConfigureWebhookAsync(phoneNumber.Sid, $"{config["VoiceAgent:PublicUrl"]}/twilio/voice?accountId={account.AccountId}");
                account.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reserving phone number for account {Account} before subscribe", account.AccountId);
                return BadRequest(new { error = "We couldn't reserve a phone number for your area right now — you have not been charged. Please try again shortly." });
            }
        }

        // Existing subscription: re-priceable ONLY if it is actually in good standing. An
        // INCOMPLETE one (abandoned card entry at signup) has never been paid — re-pricing it
        // and flipping the plan would grant a paid plan with no payment. Anything not paid-up
        // is cancelled and a fresh subscription (which collects payment) is created below.
        if (!string.IsNullOrEmpty(account.StripeSubscriptionId))
        {
            Stripe.Subscription? existing = null;
            try { existing = await stripe.GetSubscriptionAsync(account.StripeSubscriptionId); }
            catch (Exception ex) { logger.LogWarning(ex, "Stored subscription {Sub} unreadable — recreating", account.StripeSubscriptionId); }

            if (existing is { Status: "active" or "trialing" or "past_due" })
            {
                await stripe.ChangeSubscriptionPriceAsync(account.StripeSubscriptionId, priceId);
                account.PlanId = plan.PlanId;
                account.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Ok(new { status = "active" });
            }

            // A still-pending attempt at the SAME price is RESUMED — its PaymentIntent is
            // reusable, so a retry doesn't litter a new subscription per abandoned card form.
            if (existing is { Status: "incomplete" } && existing.Items?.Data?.FirstOrDefault()?.Price?.Id == priceId)
            {
                var pending = await stripe.GetDirectSubscriptionStateAsync(account.StripeSubscriptionId);
                if (pending.ClientSecret is not null)
                {
                    return Ok(new
                    {
                        status = pending.PaymentIntentStatus == "requires_action" ? "requires_action" : "requires_payment_method",
                        clientSecret = pending.ClientSecret,
                    });
                }
            }

            try { await stripe.CancelSubscriptionAsync(account.StripeSubscriptionId); }
            catch { /* already expired/canceled */ }
            account.StripeSubscriptionId = null;
        }

        // New subscription: charge the saved default card directly if there is one.
        var methods = await provider.ListPaymentMethodsAsync(customerId);
        var defaultPm = methods.FirstOrDefault(m => m.IsDefault) ?? methods.FirstOrDefault();

        var result = await stripe.CreateDirectSubscriptionAsync(customerId, priceId, defaultPm?.Id);
        account.StripeSubscriptionId = result.SubscriptionId;
        if (result.Status is "active" or "trialing")
        {
            // Paid on the spot — flip the plan now; webhooks keep the period dates in sync.
            account.PlanId = plan.PlanId;
            account.BillingPeriodStart = DateOnly.FromDateTime(DateTime.UtcNow);
            account.BillingPeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
        }
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (result.Status is "active" or "trialing")
            return Ok(new { status = "active" });
        return Ok(new
        {
            status = result.PaymentIntentStatus == "requires_action" ? "requires_action" : "requires_payment_method",
            clientSecret = result.ClientSecret,
        });
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<List<InvoiceResponse>>> GetInvoices()
    {
        var accountId = GetAccountId();
        var invoices = await db.Invoices
            .Where(i => i.AccountId == accountId)
            .OrderByDescending(i => i.DateUtc)
            .Select(i => new InvoiceResponse(
                i.InvoiceId, i.InvoiceNumber, i.DateUtc, i.Amount, i.Currency, i.InvoiceStatus!.Name, i.PdfUrl
            ))
            .ToListAsync();

        return Ok(invoices);
    }

    // Provider-agnostic payment-method management for the dashboard. The account
    // comes from the auth context. Multiple providers can be active at once: new
    // payments use the registry's Default, while existing methods are listed across
    // every provider the account already has a PaymentCustomer for.

    // Returns a client secret the frontend uses to confirm a card setup. Optionally
    // target a specific provider via ?provider=<name> (defaults to the registry Default).
    [HttpPost("setup-intent")]
    public async Task<IActionResult> CreateSetupIntent([FromQuery] string? provider)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstAsync(a => a.AccountId == accountId);

        var paymentProvider = provider is null ? payments.Default : payments.Get(provider);

        var customerId = await paymentProvider.EnsureCustomerAsync(account);
        var clientSecret = await paymentProvider.CreateSetupIntentAsync(customerId);

        return Ok(new { provider = paymentProvider.Name, clientSecret });
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var accountId = GetAccountId();

        // An account may have customers across multiple providers. List methods from
        // each PaymentCustomer the account has, tagging every method with its provider.
        var customers = await db.PaymentCustomers
            .Where(pc => pc.AccountId == accountId)
            .Include(pc => pc.Provider)
            .ToListAsync();

        var result = new List<object>();
        foreach (var customer in customers)
        {
            var paymentProvider = payments.Get(customer.Provider.Code);
            var methods = await paymentProvider.ListPaymentMethodsAsync(customer.CustomerId);
            // Duplicate-card prevention (deterministic, code-owned): the same physical card
            // has the same provider fingerprint regardless of how it was tokenized. Keep ONE
            // copy — the default if it's among the duplicates, else the oldest — detach the rest.
            var dupGroups = methods
                .Where(m => m.Fingerprint != null)
                .GroupBy(m => m.Fingerprint)
                .Where(g => g.Count() > 1)
                .ToList();
            if (dupGroups.Count > 0)
            {
                foreach (var g in dupGroups)
                {
                    var keep = g.FirstOrDefault(m => m.IsDefault) ?? g.Last(); // provider lists newest-first; Last() = oldest
                    foreach (var dup in g.Where(m => m.Id != keep.Id))
                        await paymentProvider.DetachPaymentMethodAsync(dup.Id);
                }
                methods = await paymentProvider.ListPaymentMethodsAsync(customer.CustomerId);
            }
            // Common practice, enforced self-healingly on read: a customer WITH cards but NO
            // default gets the first promoted automatically — covers the just-added first card
            // (Stripe doesn't default SetupIntent cards on its own) and a deleted default.
            if (methods.Count > 0 && !methods.Any(m => m.IsDefault))
            {
                await paymentProvider.SetDefaultPaymentMethodAsync(customer.CustomerId, methods[0].Id);
                methods = [.. methods.Select((m, i) => i == 0 ? m with { IsDefault = true } : m)];
            }
            result.AddRange(methods.Select(m => new
            {
                id = m.Id,
                brand = m.Brand,
                last4 = m.Last4,
                expMonth = m.ExpMonth,
                expYear = m.ExpYear,
                provider = paymentProvider.Name,
                isDefault = m.IsDefault,
            }));
        }

        return Ok(result);
    }

    [HttpDelete("payment-methods/{id}")]
    public async Task<IActionResult> DeletePaymentMethod(string id, [FromQuery] string? provider)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstAsync(a => a.AccountId == accountId);

        var paymentProvider = provider is null ? payments.Default : payments.Get(provider);

        // Verify the payment method belongs to this account's customer (on the given
        // provider) before detaching.
        var customerId = await paymentProvider.EnsureCustomerAsync(account);
        var methods = await paymentProvider.ListPaymentMethodsAsync(customerId);
        if (!methods.Any(m => m.Id == id))
            return NotFound(new { error = "Payment method not found" });

        await paymentProvider.DetachPaymentMethodAsync(id);
        return NoContent();
    }

    // Mark one of the account's saved payment methods as the default for future
    // charges (provider-agnostic; ownership verified before the provider call).
    [HttpPost("payment-methods/{id}/default")]
    public async Task<IActionResult> SetDefaultPaymentMethod(string id, [FromQuery] string? provider)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstAsync(a => a.AccountId == accountId);

        var paymentProvider = provider is null ? payments.Default : payments.Get(provider);

        var customerId = await paymentProvider.EnsureCustomerAsync(account);
        var methods = await paymentProvider.ListPaymentMethodsAsync(customerId);
        if (!methods.Any(m => m.Id == id))
            return NotFound(new { error = "Payment method not found" });

        await paymentProvider.SetDefaultPaymentMethodAsync(customerId, id);
        return NoContent();
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
