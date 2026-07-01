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

        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.StripeCustomerId == session.CustomerId);
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

        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.StripeCustomerId == invoice.CustomerId);
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
        if (string.IsNullOrEmpty(account.PhoneNumberSid) && account.IsEmailConfirmed)
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

                    account.PhoneNumberSid = phoneNumber.Sid;
                    account.TelephonyProvider = telephony.ProviderName;
                    account.ForwardingPhone = phoneNumber.Number;

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
        if (invoice.BillingReason == "subscription_create" && !string.IsNullOrEmpty(account.ForwardingPhone))
        {
            // Email the customer their new phone number.
            try
            {
                await email.SendPhoneNumberAssignedAsync(account.Email, account.BusinessName, account.ForwardingPhone);
            }
            catch (Exception emailEx)
            {
                logger.LogError(emailEx, "Failed to send phone number email to account {Account}", account.AccountId);
            }

            // Also notify the owner by SMS.
            var ownerPhone = account.PhoneNumber ?? account.BusinessPhone;
            if (!string.IsNullOrWhiteSpace(ownerPhone))
            {
                try
                {
                    await smsProvider.SendAsync(ownerPhone, $"Welcome to Gigahoo!\n\nHi {account.BusinessName}, your dedicated phone number is ready to receive calls:\n{account.ForwardingPhone}\n\nNext steps:\n1. Forward your existing business calls to this number\n2. Test the AI receptionist by calling the number yourself\n3. Configure your business details in the dashboard\n\nNeed help? Contact us at contact@gigahoo.ai");
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
        if (invoice is null) return;
        logger.LogWarning("Payment failed for invoice {InvoiceId}, customer {CustomerId}", invoice.Id, invoice.CustomerId);
    }

    private async Task HandleSubscriptionUpdated(Subscription? subscription)
    {
        if (subscription is null) return;

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.StripeSubscriptionId == subscription.Id);
        if (account is null) return;

        if (subscription.Status == "active")
        {
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
        if (!string.IsNullOrEmpty(account.PhoneNumberSid))
        {
            try
            {
                await twilio.ReleaseNumberFromAccountAsync(account.AccountId);
                account.PhoneNumberSid = null;
                account.TelephonyProvider = null;
                account.ForwardingPhone = null;
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
