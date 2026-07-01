namespace Gigahoo.Api.Entities;

// Lookup table of invoice payment states. Seeded with explicit ids
// (must match the InvoiceStatusId enum / DB seed rows).
public class InvoiceStatus
{
    public byte InvoiceStatusId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Invoice> Invoices { get; set; } = [];
}

public enum InvoiceStatusId : byte
{
    Paid = 1,
    Open = 2,
    Failed = 3,
    Void = 4,
}
