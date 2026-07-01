namespace Gigahoo.Api.Entities;

public class Account
{
    public Guid AccountId { get; set; }

    // Auth identity (merged from User)
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NormalizedPhone { get; set; }
    public string? GoogleSubjectId { get; set; }
    public string? PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public bool IsPhoneConfirmed { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Business profile
    public string? BusinessName { get; set; }
    public byte? CategoryId { get; set; }
    public string? BusinessPhone { get; set; }
    public string PhoneCountryCode { get; set; } = "US";
    public string? ServiceArea { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? BusinessHours { get; set; }
    public string? ForwardingPhone { get; set; }
    // Preferred dashboard/website language (BCP-47-ish locale, e.g. "en", "es",
    // "yue"). Defaults to the locale the user signed up in. NULL = not set.
    public string? AccountLanguage { get; set; }
    public byte PlanId { get; set; } = 2;

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public short? RegionId { get; set; }
    public string? RegionCustom { get; set; }
    public string? PostalCode { get; set; }
    public short? CountryCodeId { get; set; }

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public int? LlmProviderId { get; set; }
    public string? PhoneNumberSid { get; set; }
    public string? TelephonyProvider { get; set; }
    public DateOnly? BillingPeriodStart { get; set; }
    public DateOnly? BillingPeriodEnd { get; set; }
    public int MinutesUsed { get; set; }
    public DateTime? LimitNotifiedAt { get; set; }

    // Per-account post-call notification preferences (owner gets a summary per call).
    public bool EmailCallNotifications { get; set; } = true;
    public bool SmsCallNotifications { get; set; } = true;

    // AI voice agent settings: custom call greeting + selected Qwen voice (NULL = default).
    public string? GreetingMessage { get; set; }
    public string? AgentVoice { get; set; }

    // Feature settings (Business plan only)
    public bool AnswerQuestions { get; set; }
    public string? ServicesInfo { get; set; }
    public string? FeatureServiceAreas { get; set; }
    public string? FeatureBusinessHours { get; set; }
    public string? EmergencyAvailability { get; set; }
    public string? PricingPolicy { get; set; }
    public string? WarrantyPolicy { get; set; }
    public string? FrequentlyAskedQuestions { get; set; }
    public string? AdditionalBusinessInfo { get; set; }
    public bool ServeArea { get; set; }
    public int DistanceKm { get; set; } = 50;
    public bool QuoteInspection { get; set; }
    public decimal PricePerKm { get; set; }
    public DateTime FeatureUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Plan Plan { get; set; } = null!;
    public Provider? LlmProvider { get; set; }
    public BusinessCategory? Category { get; set; }
    public Region? Region { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
}
