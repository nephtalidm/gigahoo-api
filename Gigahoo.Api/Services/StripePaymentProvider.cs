using Gigahoo.Api.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Account = Gigahoo.Api.Entities.Account;
using PaymentCustomer = Gigahoo.Api.Entities.PaymentCustomer;

namespace Gigahoo.Api.Services;

// Stripe implementation of the payment-provider seam. Stores the Stripe customer id
// in PaymentCustomer (provider "stripe") and, for backward-compat with existing
// code/transition, mirrors it onto Account.StripeCustomerId.
public class StripePaymentProvider(GigahooDbContext db, IConfiguration config) : IPaymentProvider
{
    public string Name => "stripe";

    public async Task<string> EnsureCustomerAsync(Account account)
    {
        var existing = await db.PaymentCustomers
            .FirstOrDefaultAsync(pc => pc.AccountId == account.Id && pc.Provider == Name);
        if (existing is not null)
            return existing.CustomerId;

        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        var service = new CustomerService();
        var customer = service.Create(new CustomerCreateOptions
        {
            Email = account.Email,
            Name = account.BusinessName,
        });

        db.PaymentCustomers.Add(new PaymentCustomer
        {
            AccountId = account.Id,
            Provider = Name,
            CustomerId = customer.Id,
        });

        // Backward-compat: keep Account.StripeCustomerId populated during transition.
        account.StripeCustomerId = customer.Id;

        await db.SaveChangesAsync();
        return customer.Id;
    }

    public Task<string> CreateSetupIntentAsync(string customerId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        var service = new SetupIntentService();
        var intent = service.Create(new SetupIntentCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = ["card"],
        });
        return Task.FromResult(intent.ClientSecret);
    }

    public Task<IReadOnlyList<PaymentMethodInfo>> ListPaymentMethodsAsync(string customerId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        var service = new PaymentMethodService();
        var methods = service.List(new PaymentMethodListOptions
        {
            Customer = customerId,
            Type = "card",
        });

        IReadOnlyList<PaymentMethodInfo> result = methods.Data
            .Select(pm => new PaymentMethodInfo(
                pm.Id,
                pm.Card?.Brand ?? "",
                pm.Card?.Last4 ?? "",
                pm.Card?.ExpMonth ?? 0,
                pm.Card?.ExpYear ?? 0))
            .ToList();
        return Task.FromResult(result);
    }

    public Task DetachPaymentMethodAsync(string paymentMethodId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        var service = new PaymentMethodService();
        service.Detach(paymentMethodId);
        return Task.CompletedTask;
    }
}
