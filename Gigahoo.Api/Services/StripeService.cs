using Stripe;

namespace Gigahoo.Api.Services;

public interface IStripeService
{
    Task<string> CreateCustomerAsync(string email, string businessName);
    Task<string> CreateSubscriptionAsync(string customerId, string priceId);
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
            Items = [{ Price = priceId }],
            PaymentBehavior = "default_incomplete",
        };
        options.AddExpand("latest_invoice.payment_intent");

        var service = new SubscriptionService();
        var subscription = service.Create(options);
        return Task.FromResult(subscription.Id);
    }

    public Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
        };

        var service = new SessionService();
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
