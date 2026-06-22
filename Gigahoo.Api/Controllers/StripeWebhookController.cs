using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
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
    ITwilioService twilio) : ControllerBase
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

    private async Task HandleInvoicePaid(Stripe.Invoice? invoice)
    {
        if (invoice is null) return;

        var account = await db.Accounts
            .Include(a => a.Country)
            .FirstOrDefaultAsync(a => a.StripeCustomerId == invoice.CustomerId);
        if (account is null) return;

        // Record the invoice
        db.Invoices.Add(new Entities.Invoice
        {
            AccountId = account.Id,
            StripeInvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number ?? $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            DateUtc = DateTime.UtcNow,
            Amount = invoice.AmountPaid / 100m,
            Currency = invoice.Currency?.ToUpper() ?? "USD",
            Status = "Paid",
            PdfUrl = invoice.HostedInvoiceUrl,
            CreatedAt = DateTime.UtcNow,
        });

        // Provision phone number if not already done
        if (string.IsNullOrEmpty(account.PhoneNumberSid))
        {
            try
            {
                var countryCode = account.Country?.Code ?? "US";
                var phoneNumberSid = await twilio.PurchasePhoneNumberAsync(countryCode);

                if (phoneNumberSid is not null)
                {
                    account.PhoneNumberSid = phoneNumberSid;
                    account.TelephonyProvider = "twilio";

                    // Configure webhook to point to voice agent
                    var webhookUrl = $"{config["VoiceAgent:PublicUrl"]}/twilio/voice?accountId={account.Id}";
                    await twilio.ConfigureWebhookAsync(phoneNumberSid, webhookUrl);

                    logger.LogInformation("Provisioned phone number for account {Account}: {PhoneNumberSid}", account.Id, phoneNumberSid);
                }
                else
                {
                    logger.LogWarning("Failed to provision phone number for account {Account}", account.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error provisioning phone number for account {Account}", account.Id);
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Invoice {Id} recorded for account {Account}", invoice.Id, account.Id);
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
            var period = subscription.CurrentPeriodEnd;
            if (period != default)
            {
                account.BillingPeriodEnd = DateOnly.FromDateTime(period);
            }
            await db.SaveChangesAsync();
        }
    }

    private async Task HandleSubscriptionDeleted(Subscription? subscription)
    {
        if (subscription is null) return;

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.StripeSubscriptionId == subscription.Id);
        if (account is null) return;

        // Release phone number if exists
        if (!string.IsNullOrEmpty(account.PhoneNumberSid))
        {
            try
            {
                await twilio.ReleasePhoneNumberAsync(account.PhoneNumberSid);
                account.PhoneNumberSid = null;
                account.TelephonyProvider = null;
                logger.LogInformation("Released phone number for account {Account}", account.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error releasing phone number for account {Account}", account.Id);
            }
        }

        account.StripeSubscriptionId = null;
        account.PlanId = 1; // Downgrade to Free
        await db.SaveChangesAsync();

        logger.LogInformation("Account {Account} downgraded to Free after subscription cancellation", account.Id);
    }
}
