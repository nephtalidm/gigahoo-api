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
    ITwilioService twilio,
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
                config[$"Stripe:PriceIds:{p.Id}"] == priceId);
            if (plan is not null)
                account.PlanId = plan.Id;

            account.BillingPeriodStart = DateOnly.FromDateTime(DateTime.UtcNow);
            account.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            logger.LogInformation("Checkout completed for account {Account}, subscription {Sub}", account.Id, session.SubscriptionId);
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

        // Provision phone number if not already done and email is verified
        if (string.IsNullOrEmpty(account.PhoneNumberSid) && account.IsEmailConfirmed)
        {
            try
            {
                var countryCode = await db.Countries.FindAsync(account.CountryCodeId) is { } c ? c.Code : "US";
                
                // First, try to get an available number from the pool
                var phoneNumber = await twilio.GetAvailableNumberAsync(countryCode);
                
                if (phoneNumber == null)
                {
                    // No available number in pool, purchase a new one
                    var phoneNumberSid = await twilio.PurchasePhoneNumberAsync(countryCode);
                    
                    if (phoneNumberSid is not null)
                    {
                        // Get the phone number details from Twilio
                        Twilio.TwilioClient.Init(config["Twilio:AccountSid"]!, config["Twilio:AuthToken"]!);
                        var twilioNumber = await Twilio.Rest.Api.V2010.Account.IncomingPhoneNumberResource.FetchAsync(phoneNumberSid);
                        
                        // Add to pool
                        phoneNumber = new Entities.PhoneNumber
                        {
                            Sid = phoneNumberSid,
                            Number = twilioNumber.PhoneNumber.ToString(),
                            CountryCode = countryCode,
                            Provider = "twilio",
                            Status = Entities.PhoneNumberStatus.Available,
                            MonthlyCost = 1.15m,
                            PurchasedAt = DateTime.UtcNow
                        };
                        db.PhoneNumbers.Add(phoneNumber);
                        await db.SaveChangesAsync();
                        
                        logger.LogInformation("Purchased new phone number {Number} and added to pool", twilioNumber.PhoneNumber);
                    }
                    else
                    {
                        logger.LogWarning("Failed to purchase phone number for account {Account}", account.Id);
                    }
                }
                
                if (phoneNumber != null)
                {
                    // Assign number to account
                    await twilio.AssignNumberToAccountAsync(phoneNumber, account.Id);

                    account.PhoneNumberSid = phoneNumber.Sid;
                    account.TelephonyProvider = phoneNumber.Provider;
                    account.ForwardingPhone = phoneNumber.Number;

                    // Configure webhook to point to voice agent
                    var webhookUrl = $"{config["VoiceAgent:PublicUrl"]}/twilio/voice?accountId={account.Id}";
                    await twilio.ConfigureWebhookAsync(phoneNumber.Sid, webhookUrl);

                    // Send email to customer with their phone number
                    try
                    {
                        await email.SendPhoneNumberAssignedAsync(account.Email, account.BusinessName, phoneNumber.Number);
                    }
                    catch (Exception emailEx)
                    {
                        logger.LogError(emailEx, "Failed to send phone number email to account {Account}", account.Id);
                    }

                    logger.LogInformation("Assigned phone number {Number} to account {Account}", phoneNumber.Number, account.Id);
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

        // Release phone number back to pool (don't delete from Twilio)
        if (!string.IsNullOrEmpty(account.PhoneNumberSid))
        {
            try
            {
                await twilio.ReleaseNumberFromAccountAsync(account.Id);
                account.PhoneNumberSid = null;
                account.TelephonyProvider = null;
                account.ForwardingPhone = null;
                logger.LogInformation("Released phone number back to pool for account {Account}", account.Id);
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
