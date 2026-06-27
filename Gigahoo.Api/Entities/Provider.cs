namespace Gigahoo.Api.Entities;

// A concrete provider (e.g. Stripe, Qwen, Twilio, SendGrid) of a given ProviderType.
// Code is the stable machine identifier (matches IPaymentProvider.Name etc.); rows
// that PlanPrice / PaymentCustomer / Account reference by Id instead of magic strings.
public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;   // display name, e.g. "Stripe"
    public string Code { get; set; } = null!;   // machine code, e.g. "stripe"
    public byte ProviderTypeId { get; set; }
    public ProviderType ProviderType { get; set; } = null!;
}
