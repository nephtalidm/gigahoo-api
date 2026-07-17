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
    public byte? BusinessCategoryId { get; set; }
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
    public string? PostalCode { get; set; }
    public short? CountryCodeId { get; set; }

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? PhoneNumberSid { get; set; }
    public string? TelephonyProvider { get; set; }
    public DateOnly? BillingPeriodStart { get; set; }
    public DateOnly? BillingPeriodEnd { get; set; }
    public int MinutesUsed { get; set; }
    public DateTime? LimitNotifiedAt { get; set; }

    // Per-account post-call notification preferences (owner gets a summary per call).
    public bool EmailCallNotifications { get; set; } = true;
    public bool SmsCallNotifications { get; set; } = true;

    // Per-account "Questions" — which details the agent collects (default on). Off = don't ask/collect, and hide downstream.
    public bool CollectName { get; set; } = true;
    public bool CollectPhone { get; set; } = true;
    public bool CollectAddress { get; set; } = true;
    public bool CollectEmergency { get; set; } = true;

    // AI voice agent settings: custom call greeting + selected Qwen voice (NULL = default).
    public string? GreetingMessage { get; set; }
    public string? AgentVoice { get; set; }
    // Voice emotion (CosyVoice native emotion value: neutral|happy|sad|angry|fearful|surprised|disgusted).
    public string? AgentStyle { get; set; }
    // Optional CosyVoice instruct context key ("scenario:…"|"role:…"|"identity:…"); pairs with AgentStyle.
    public string? AgentInstruct { get; set; }

    // Per-call hard cap (kill switch): the longest a single call may run before the voice agent
    // forcibly ends it, no matter how productive the call is. Minutes. Defaults to 10;
    // NULL = Unlimited (an explicit opt-out chosen from the dashboard).
    public int? MaximumCallMinutes { get; set; } = 10;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Plan Plan { get; set; } = null!;
    public BusinessCategory? Category { get; set; }
    public Region? Region { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
}
