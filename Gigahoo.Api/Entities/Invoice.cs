namespace Gigahoo.Api.Entities;

public class Invoice
{
    public Guid InvoiceId { get; set; }
    public Guid AccountId { get; set; }
    public string? StripeInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime DateUtc { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public byte InvoiceStatusId { get; set; } = 1; // FK -> InvoiceStatus (1 = Paid)
    public DateTime CreatedAt { get; set; }

    public Account Account { get; set; } = null!;
    public InvoiceStatus? InvoiceStatus { get; set; }
}
