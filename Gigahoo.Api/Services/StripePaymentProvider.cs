using Gigahoo.Api.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Account = Gigahoo.Api.Entities.Account;
using PaymentCustomer = Gigahoo.Api.Entities.PaymentCustomer;

namespace Gigahoo.Api.Services;

// Stripe implementation of the payment-provider seam. The Stripe customer id lives in
// PaymentCustomer (provider "stripe") — the ONE source of truth for provider identities.
public class StripePaymentProvider(GigahooDbContext db, IConfiguration config) : IPaymentProvider
{
    public string Name => "stripe";

    public async Task<string> EnsureCustomerAsync(Account account)
    {
        // Resolve this provider's Provider row (Code == Name, Payment type) once.
        var providerRow = await db.Providers
            .FirstOrDefaultAsync(p => p.Code == Name && p.ProviderTypeId == (byte)Entities.ProviderTypeId.Payment)
            ?? throw new InvalidOperationException(
                $"No Provider row found for payment provider '{Name}'. Run the provider-tables migration.");

        var existing = await db.PaymentCustomers
            .FirstOrDefaultAsync(pc => pc.AccountId == account.AccountId && pc.ProviderId == providerRow.ProviderId);
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
            AccountId = account.AccountId,
            ProviderId = providerRow.ProviderId,
            CustomerId = customer.Id,
        });

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

        // The default lives on the customer (invoice settings), not the payment method.
        var customer = new CustomerService().Get(customerId);
        var defaultId = customer.InvoiceSettings?.DefaultPaymentMethodId;

        IReadOnlyList<PaymentMethodInfo> result = methods.Data
            .Select(pm => new PaymentMethodInfo(
                pm.Id,
                pm.Card?.Brand ?? "",
                pm.Card?.Last4 ?? "",
                pm.Card?.ExpMonth ?? 0,
                pm.Card?.ExpYear ?? 0,
                pm.Id == defaultId,
                pm.Card?.Fingerprint))
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

    public Task SetDefaultPaymentMethodAsync(string customerId, string paymentMethodId)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        new CustomerService().Update(customerId, new CustomerUpdateOptions
        {
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                DefaultPaymentMethod = paymentMethodId,
            },
        });
        return Task.CompletedTask;
    }
}
