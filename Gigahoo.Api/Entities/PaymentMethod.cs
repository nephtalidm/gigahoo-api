namespace Gigahoo.Api.Entities;

public class PaymentMethod
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string StripePaymentMethodId { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Last4 { get; set; } = null!;
    public byte ExpMonth { get; set; }
    public short ExpYear { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }

    public Account Account { get; set; } = null!;
}
