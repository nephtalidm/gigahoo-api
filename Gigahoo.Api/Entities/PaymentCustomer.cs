namespace Gigahoo.Api.Entities;

// One row per (account, payment provider) holding that provider's customer id.
// Provider-agnostic replacement for Account.StripeCustomerId so additional
// payment providers can be added without schema churn.
public class PaymentCustomer
{
    public int PaymentCustomerId { get; set; }
    public Guid AccountId { get; set; }
    public int ProviderId { get; set; }                // FK -> Provider (payment provider)
    public string CustomerId { get; set; } = null!;    // the provider's customer id
    public Provider Provider { get; set; } = null!;
}
