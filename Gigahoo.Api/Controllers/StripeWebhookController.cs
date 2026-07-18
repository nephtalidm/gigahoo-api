using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
using Gigahoo.Api.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController(
    GigahooDbContext db,
    IConfiguration config,
    ILogger<StripeWebhookController> logger,
    ITwilioService twilio,
    ITelephonyProvider telephony,
    ISmsProvider smsProvider,
    IEmailService email) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        Event stripeEvent;
        try
        {
            StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
            stripeEvent = EventUtility.ConstructEvent(
                json, signature, config["Stripe:WebhookSecret"]!);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest();
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompleted(stripeEvent.Data.Object as Stripe.Checkout.Session);
                break;
            case "invoice.paid":
                await HandleInvoicePaid(stripeEvent.Data.Object as Stripe.Invoice);
                break;
            case "invoice.payment_failed":
                await HandlePaymentFailed(stripeEvent.Data.Object as Stripe.Invoice);
                break;
            case "customer.subscription.updated":
                await HandleSubscriptionUpdated(stripeEvent.Data.Object as Subscription);
                break;
            case "customer.subscription.deleted":
                await HandleSubscriptionDeleted(stripeEvent.Data.Object as Subscription);
                break;
            default:
                logger.LogInformation("Unhandled Stripe event type: {Type}", stripeEvent.Type);
                break;
        }

        return Ok();
    }

    private async Task HandleCheckoutCompleted(Stripe.Checkout.Session? session)
    {
        if (session is null) return;

        var accountId = await db.PaymentCustomers
            .Where(pc => pc.CustomerId == session.CustomerId)
            .Select(pc => (Guid?)pc.AccountId)
            .FirstOrDefaultAsync();
        var account = accountId is null
            ? null
            : await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
        if (account is null) return;

        // Save subscription ID and upgrade plan
        if (session.SubscriptionId is not null)
        {
            account.StripeSubscriptionId = session.SubscriptionId;

            // Map plan from Stripe metadata or default to Starter
            var priceId = session.Metadata?.GetValueOrDefault("priceId");
            var plan = await db.Plans.FirstOrDefaultAsync(p =>
                config[$"Stripe:PriceIds:{p.PlanId}"] == priceId);
            if (plan is not null)
                account.PlanId = plan.PlanId;

            // Initialize the billing period and reset usage metering for the new subscription.
            account.BillingPeriodStart = DateOnly.FromDateTime(DateTime.UtcNow);
            account.BillingPeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
            account.MinutesUsed = 0;
            account.LimitNotifiedAt = null;
            account.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            logger.LogInformation("Checkout completed for account {Account}, subscription {Sub}", account.AccountId, session.SubscriptionId);
        }
    }

    private async Task HandleInvoicePaid(Stripe.Invoice? invoice)
    {
        if (invoice is null) return;

        var invAccountId = await db.PaymentCustomers
            .Where(pc => pc.CustomerId == invoice.CustomerId)
            .Select(pc => (Guid?)pc.AccountId)
            .FirstOrDefaultAsync();
        var account = invAccountId is null
            ? null
            : await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == invAccountId);
        if (account is null) return;

        // Record the invoice
        db.Invoices.Add(new Entities.Invoice
        {
            AccountId = account.AccountId,
            StripeInvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number ?? $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            DateUtc = DateTime.UtcNow,
            Amount = invoice.AmountPaid / 100m,
            Currency = invoice.Currency?.ToUpper() ?? "USD",
            InvoiceStatusId = (byte)Entities.InvoiceStatusId.Paid,
            PdfUrl = invoice.HostedInvoiceUrl,
            CreatedAt = DateTime.UtcNow,
        });

        // Renewal: a paid invoice starts a fresh billing period — reset usage metering.
        account.MinutesUsed = 0;
        account.LimitNotifiedAt = null;
        account.BillingPeriodStart = DateOnly.FromDateTime(DateTime.UtcNow);
        account.BillingPeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
        account.UpdatedAt = DateTime.UtcNow;

        // Reserve-then-charge: the number is normally secured during checkout
        // (BillingController), before payment. This is a fallback for the rare case
        // where the reservation didn't happen — provision now so the account isn't
        // left without a number after paying.
        if (account.AssignedPhoneNumberId is null && account.IsEmailConfirmed)
        {
            try
            {
                var countryCode = await db.Countries.FindAsync(account.CountryCodeId) is { } c ? c.Code : "US";

                // First, try to get an available number from the pool
                var phoneNumber = await twilio.GetAvailableNumberAsync(countryCode);

                if (phoneNumber == null)
                {
                    // No available number in pool, purchase a new one via the configured carrier.
                    var purchased = await twilio.PurchasePhoneNumberAsync(countryCode);

                    if (purchased is not null)
                    {
                        // Add to pool
                        phoneNumber = await twilio.AddPurchasedNumberToPoolAsync(purchased, countryCode);
                        logger.LogInformation("Purchased new phone number {Number} and added to pool", purchased.PhoneNumber);
                    }
                    else
                    {
                        logger.LogWarning("Failed to purchase phone number for account {Account}", account.AccountId);
                    }
                }

                if (phoneNumber != null)
                {
                    // Assign number to account
                    await twilio.AssignNumberToAccountAsync(phoneNumber, account.AccountId);

                    account.AssignedPhoneNumberId = phoneNumber.PhoneNumberId;

                    // Configure webhook to point to voice agent
                    var webhookUrl = $"{config["VoiceAgent:PublicUrl"]}/twilio/voice?accountId={account.AccountId}";
                    await twilio.ConfigureWebhookAsync(phoneNumber.Sid, webhookUrl);

                    logger.LogInformation("Assigned phone number {Number} to account {Account} (fallback at invoice.paid)", phoneNumber.Number, account.AccountId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error provisioning phone number for account {Account}", account.AccountId);
            }
        }

        // Notify the owner of their new number only on the FIRST invoice of the
        // subscription (subscription_create) — never on renewals (subscription_cycle).
        var assignedNumber = account.AssignedPhoneNumberId is null ? null : await db.PhoneNumbers
            .Where(p => p.PhoneNumberId == account.AssignedPhoneNumberId)
            .Select(p => p.Number)
            .FirstOrDefaultAsync();
        if (invoice.BillingReason == "subscription_create" && !string.IsNullOrEmpty(assignedNumber))
        {
            // Email the customer their new phone number.
            try
            {
                await email.SendPhoneNumberAssignedAsync(account.Email, account.BusinessName, assignedNumber);
            }
            catch (Exception emailEx)
            {
                logger.LogError(emailEx, "Failed to send phone number email to account {Account}", account.AccountId);
            }

            // Also notify the owner by SMS.
            var ownerPhone = account.BusinessPhoneNumber;
            if (!string.IsNullOrWhiteSpace(ownerPhone))
            {
                try
                {
                    await smsProvider.SendAsync(ownerPhone, $"Welcome to Gigahoo!\n\nHi {account.BusinessName}, your dedicated phone number is ready to receive calls:\n{assignedNumber}\n\nNext steps:\n1. Forward your existing business calls to this number\n2. Test the AI receptionist by calling the number yourself\n3. Configure your business details in the dashboard\n\nNeed help? Contact us at contact@gigahoo.ai");
                }
                catch (Exception smsEx)
                {
                    logger.LogError(smsEx, "Failed to send phone number SMS to account {Account}", account.AccountId);
                }
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Invoice {Id} recorded for account {Account}", invoice.Id, account.AccountId);
    }

    private async Task HandlePaymentFailed(Stripe.Invoice? invoice)
    {
        if (invoice is null || invoice.Id is null || invoice.CustomerId is null) return;
        logger.LogWarning("Payment failed for invoice {InvoiceId}, customer {CustomerId}", invoice.Id, invoice.CustomerId);

        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        var invoiceService = new InvoiceService();

        // Work from a FRESH copy: the webhook payload is a snapshot. A paid invoice needs
        // nothing; and our own failed fallback attempts fire invoice.payment_failed again,
        // so a metadata marker guarantees each invoice is walked exactly once.
        var fresh = await invoiceService.GetAsync(invoice.Id);
        if (fresh.Status == "paid") return;
        if (fresh.Metadata is not null && fresh.Metadata.ContainsKey("gigahoo_fallback_attempted")) return;
        await invoiceService.UpdateAsync(invoice.Id, new InvoiceUpdateOptions
        {
            Metadata = new Dictionary<string, string> { ["gigahoo_fallback_attempted"] = "true" },
        });

        // FALLBACK WALKER: Stripe's automatic retries only ever hit the DEFAULT card. Walk
        // the customer's OTHER saved cards oldest-first and try to pay THIS invoice. An
        // invoice can only be paid once, so a racing Stripe retry can never double-charge.
        var customerService = new CustomerService();
        var customer = await customerService.GetAsync(invoice.CustomerId);
        var defaultPm = customer.InvoiceSettings?.DefaultPaymentMethodId;
        var cards = await new PaymentMethodService().ListAsync(new PaymentMethodListOptions
        {
            Customer = invoice.CustomerId,
            Type = "card",
        });
        var fallbacks = cards.Data.Where(c => c.Id != defaultPm).Reverse().ToList(); // oldest first

        string? paidWith = null;
        foreach (var card in fallbacks)
        {
            try
            {
                await invoiceService.PayAsync(invoice.Id, new InvoicePayOptions { PaymentMethod = card.Id });
                paidWith = card.Id;
                // The card that worked becomes the default, so NEXT period charges it first
                // instead of failing and falling back again.
                await customerService.UpdateAsync(invoice.CustomerId, new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = card.Id },
                });
                logger.LogInformation("Invoice {InvoiceId} paid with fallback card {Card}; promoted to default", invoice.Id, card.Id);
                break;
            }
            catch (StripeException ex)
            {
                logger.LogWarning("Fallback card {Card} declined for invoice {InvoiceId}: {Message}", card.Id, invoice.Id, ex.Message);
            }
        }

        // Tell the owner what happened (best effort, never fails the webhook).
        var fallbackAccountId = await db.PaymentCustomers
            .Where(pc => pc.CustomerId == invoice.CustomerId)
            .Select(pc => (Guid?)pc.AccountId)
            .FirstOrDefaultAsync();
        var account = fallbackAccountId is null ? null : await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == fallbackAccountId);
        if (!string.IsNullOrWhiteSpace(account?.BusinessPhoneNumber))
        {
            var msg = paidWith is not null
                ? "Gigahoo: your default card was declined, so we charged your backup card and made it your new default."
                : "Gigahoo: your payment failed and none of your saved cards could be charged. Please update your payment method in the dashboard.";
            try { await smsProvider.SendAsync(account!.BusinessPhoneNumber!, msg); }
            catch (Exception ex) { logger.LogError(ex, "Failed to send payment-failed SMS for account {Account}", account!.AccountId); }
        }
    }

    private async Task HandleSubscriptionUpdated(Subscription? subscription)
    {
        if (subscription is null) return;

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.StripeSubscriptionId == subscription.Id);
        if (account is null) return;

        if (subscription.Status == "active")
        {
            // EMBEDDED subscriptions never pass through checkout.session.completed — the plan is
            // mapped from the subscription's own priceId metadata (set at create/re-price time).
            var priceId = subscription.Metadata?.GetValueOrDefault("priceId");
            if (!string.IsNullOrEmpty(priceId))
            {
                var plan = await db.Plans.FirstOrDefaultAsync(p => config[$"Stripe:PriceIds:{p.PlanId}"] == priceId);
                if (plan is not null && account.PlanId != plan.PlanId)
                    account.PlanId = plan.PlanId;
            }

            var periodStart = subscription.CurrentPeriodStart;
            var periodEnd = subscription.CurrentPeriodEnd;

            // Detect the start of a new billing period and reset usage metering.
            if (periodStart != default)
            {
                var newStart = DateOnly.FromDateTime(periodStart);
                if (account.BillingPeriodStart != newStart)
                {
                    account.BillingPeriodStart = newStart;
                    account.MinutesUsed = 0;
                    account.LimitNotifiedAt = null;
                }
            }

            if (periodEnd != default)
            {
                account.BillingPeriodEnd = DateOnly.FromDateTime(periodEnd);
            }

            account.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private async Task HandleSubscriptionDeleted(Subscription? subscription)
    {
        if (subscription is null) return;

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.StripeSubscriptionId == subscription.Id);
        if (account is null) return;

        // Release phone number back to pool (don't delete from Twilio)
        if (account.AssignedPhoneNumberId is not null)
        {
            try
            {
                await twilio.ReleaseNumberFromAccountAsync(account.AccountId);
                account.AssignedPhoneNumberId = null;
                logger.LogInformation("Released phone number back to pool for account {Account}", account.AccountId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error releasing phone number for account {Account}", account.AccountId);
            }
        }

        account.StripeSubscriptionId = null;
        account.PlanId = 1; // Downgrade to Free
        await db.SaveChangesAsync();

        logger.LogInformation("Account {Account} downgraded to Free after subscription cancellation", account.AccountId);
    }
}
