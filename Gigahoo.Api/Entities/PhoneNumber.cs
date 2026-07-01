namespace Gigahoo.Api.Entities;

public class PhoneNumber
{
    public Guid PhoneNumberId { get; set; }
    public string Sid { get; set; } = null!; // Twilio/Telnyx SID
    public string Number { get; set; } = null!; // E.164 format: +1234567890
    public short CountryId { get; set; } // FK -> Country
    public int ProviderId { get; set; } // FK -> Provider (Phone type: twilio, telnyx, ...)
    public PhoneNumberStatus Status { get; set; } = PhoneNumberStatus.Available;
    public Guid? AssignedAccountId { get; set; }
    public decimal MonthlyCost { get; set; } = 1.15m;
    public DateTime PurchasedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Account? AssignedAccount { get; set; }
    public Country? Country { get; set; }
    public Provider? Provider { get; set; }
}
