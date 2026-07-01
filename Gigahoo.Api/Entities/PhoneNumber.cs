namespace Gigahoo.Api.Entities;

public class PhoneNumber
{
    public Guid PhoneNumberId { get; set; }
    public string Sid { get; set; } = null!; // Twilio/Telnyx SID
    public string Number { get; set; } = null!; // E.164 format: +1234567890
    public short CountryId { get; set; } // FK -> Country
    public int ProviderId { get; set; } // FK -> Provider (Phone type: twilio, telnyx, ...)
    public byte PhoneNumberStatusId { get; set; } = 1; // FK -> PhoneNumberStatus (1 = Available)
    public Guid? AssignedAccountId { get; set; }
    public decimal MonthlyCost { get; set; } = 1.15m;
    public DateTime PurchasedAt { get; set; }
    public DateTime? AssignedAt { get; set; }

    // Navigation
    public Account? AssignedAccount { get; set; }
    public Country? Country { get; set; }
    public Provider? Provider { get; set; }
    public PhoneNumberStatus? PhoneNumberStatus { get; set; }
}
