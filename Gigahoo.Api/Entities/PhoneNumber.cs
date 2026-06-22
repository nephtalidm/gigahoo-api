namespace Gigahoo.Api.Entities;

public class PhoneNumber
{
    public Guid Id { get; set; }
    public string Sid { get; set; } = null!; // Twilio/Telnyx SID
    public string Number { get; set; } = null!; // E.164 format: +1234567890
    public string CountryCode { get; set; } = null!; // ISO 3166-1 alpha-2: US, CA, etc.
    public string Provider { get; set; } = "twilio"; // twilio, telnyx, etc.
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
}
