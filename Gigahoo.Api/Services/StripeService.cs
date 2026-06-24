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
}

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
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions { Metadata = { { "priceId", priceId } } },
        };

        var service = new Stripe.Checkout.SessionService();
        var session = service.Create(options);
        return Task.FromResult(session.Url);
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
