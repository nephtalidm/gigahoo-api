using Stripe;

namespace Gigahoo.Api.Services;

public interface IStripeService
{
    Task<string> CreateCustomerAsync(string email, string businessName);
    Task<string> CreateSubscriptionAsync(string customerId, string priceId);
    Task<string> CreateCheckoutSessionAsync(string customerId, string priceId, string successUrl, string cancelUrl);
    Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl);
    Task CancelSubscriptionAsync(string subscriptionId);
    Task<Subscription> GetSubscriptionAsync(string subscriptionId);
    Task<DirectSubscriptionResult> CreateDirectSubscriptionAsync(string customerId, string priceId, string? defaultPaymentMethodId);
    Task ChangeSubscriptionPriceAsync(string subscriptionId, string priceId);
}

/// <summary>Outcome of an EMBEDDED (no hosted page) subscription creation.</summary>
public record DirectSubscriptionResult(string SubscriptionId, string Status, string? PaymentIntentStatus, string? ClientSecret);

public class StripeService(IConfiguration config) : IStripeService
{
    public Task<string> CreateCustomerAsync(string email, string businessName)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = businessName,
        };

        var service = new CustomerService();
        var customer = service.Create(options);
        return Task.FromResult(customer.Id);
    }

    public Task<string> CreateSubscriptionAsync(string customerId, string priceId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var options = new SubscriptionCreateOptions
        {
            Customer = customerId,
            Items = new List<SubscriptionItemOptions> { new() { Price = priceId } },
            PaymentBehavior = "default_incomplete",
        };
        options.AddExpand("latest_invoice.payment_intent");

        var service = new SubscriptionService();
        var subscription = service.Create(options);
        return Task.FromResult(subscription.Id);
    }

    public Task<string> CreateCheckoutSessionAsync(string customerId, string priceId, string successUrl, string cancelUrl)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var options = new Stripe.Checkout.SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            PaymentMethodTypes = ["card"],
            LineItems = [new Stripe.Checkout.SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                // Metadata defaults to null on the SDK options — it must be newed up; a bare
                // collection initializer NREs before Stripe is ever called.
                Metadata = new Dictionary<string, string> { { "priceId", priceId } },
            },
        };

        var service = new Stripe.Checkout.SessionService();
        var session = service.Create(options);
        return Task.FromResult(session.Url);
    }

    public async Task<DirectSubscriptionResult> CreateDirectSubscriptionAsync(string customerId, string priceId, string? defaultPaymentMethodId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var options = new SubscriptionCreateOptions
        {
            Customer = customerId,
            Items = [new SubscriptionItemOptions { Price = priceId }],
            // Saved card: attempt the charge NOW ("allow_incomplete" surfaces requires_action for
            // 3DS instead of failing). No card: "default_incomplete" leaves an unconfirmed
            // PaymentIntent that the in-app card form confirms.
            PaymentBehavior = defaultPaymentMethodId is null ? "default_incomplete" : "allow_incomplete",
            DefaultPaymentMethod = defaultPaymentMethodId,
            // A card entered in the in-app form becomes the subscription's default for renewals.
            PaymentSettings = new SubscriptionPaymentSettingsOptions { SaveDefaultPaymentMethod = "on_subscription" },
            // The plan flip is webhook-driven and data-mapped from this priceId (no checkout
            // session exists on the embedded path).
            Metadata = new Dictionary<string, string> { { "priceId", priceId } },
        };
        options.AddExpand("latest_invoice.payment_intent");

        var service = new SubscriptionService();
        var subscription = await service.CreateAsync(options);
        var intent = subscription.LatestInvoice?.PaymentIntent;
        return new DirectSubscriptionResult(subscription.Id, subscription.Status, intent?.Status, intent?.ClientSecret);
    }

    public async Task ChangeSubscriptionPriceAsync(string subscriptionId, string priceId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var service = new SubscriptionService();
        var subscription = await service.GetAsync(subscriptionId);
        await service.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
        {
            // Swap the single plan item in place; prorate the difference on the next invoice.
            Items = [new SubscriptionItemOptions { Id = subscription.Items.Data[0].Id, Price = priceId }],
            ProrationBehavior = "create_prorations",
            PaymentBehavior = "allow_incomplete",
            Metadata = new Dictionary<string, string> { { "priceId", priceId } },
        });
    }

    public Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
        };

        var service = new Stripe.BillingPortal.SessionService();
        var session = service.Create(options);
        return Task.FromResult(session.Url);
    }

    public Task CancelSubscriptionAsync(string subscriptionId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var service = new SubscriptionService();
        service.Cancel(subscriptionId);
        return Task.CompletedTask;
    }

    public Task<Subscription> GetSubscriptionAsync(string subscriptionId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var service = new SubscriptionService();
        var subscription = service.Get(subscriptionId);
        return Task.FromResult(subscription);
    }
}
