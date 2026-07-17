using Gigahoo.Api.Entities;

namespace Gigahoo.Api.Services;

// Swappable seam for payment providers. Multiple providers can be active at once:
// every implementation is registered and held by IPaymentProviderRegistry, which
// resolves them by Name and picks the Default ("Payments:DefaultProvider") for new
// payments.
public interface IPaymentProvider
{
    string Name { get; }                                            // e.g. "stripe"

    // Get-or-create the provider customer id for this account, persisted in
    // PaymentCustomer for this provider. Auto-creates even for free-plan accounts
    // that have no customer yet.
    Task<string> EnsureCustomerAsync(Account account);

    // Create a setup intent for adding a payment method; returns the client secret.
    Task<string> CreateSetupIntentAsync(string customerId);

    Task<IReadOnlyList<PaymentMethodInfo>> ListPaymentMethodsAsync(string customerId);

    Task DetachPaymentMethodAsync(string paymentMethodId);

    // Mark a saved payment method as the customer's default for future charges.
    Task SetDefaultPaymentMethodAsync(string customerId, string paymentMethodId);
}

public record PaymentMethodInfo(string Id, string Brand, string Last4, long ExpMonth, long ExpYear, bool IsDefault = false, string? Fingerprint = null);
