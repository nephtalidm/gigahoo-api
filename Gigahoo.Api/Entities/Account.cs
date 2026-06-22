namespace Gigahoo.Api.Entities;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BusinessName { get; set; } = null!;
    public byte CategoryId { get; set; }
    public string BusinessPhone { get; set; } = null!;
    public string PhoneCountryCode { get; set; } = "US";
    public string Email { get; set; } = null!;
    public string? ServiceArea { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? BusinessHours { get; set; }
    public string? ForwardingPhone { get; set; }
    public byte PlanId { get; set; } = 2;

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public short? RegionId { get; set; }
    public string? RegionCustom { get; set; }
    public string? PostalCode { get; set; }
    public short CountryId { get; set; }

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? PhoneNumberSid { get; set; }
    public string? TelephonyProvider { get; set; }
    public DateOnly? BillingPeriodStart { get; set; }
    public DateOnly? BillingPeriodEnd { get; set; }
    public int MinutesUsed { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
    public BusinessCategory Category { get; set; } = null!;
    public Country Country { get; set; } = null!;
    public Region? Region { get; set; }
    public FeatureSettings? FeatureSettings { get; set; }
    public ICollection<Call> Calls { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<PaymentMethod> PaymentMethods { get; set; } = [];
}
