namespace Gigahoo.Api.Entities;

public class Account
{
    public Guid AccountId { get; set; }

    // Auth identity (merged from User)
    public string? Email { get; set; }
    public string? GoogleSubjectId { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public bool IsPhoneConfirmed { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Business profile
    public string? BusinessName { get; set; }
    public byte? BusinessCategoryId { get; set; }
    public string? BusinessPhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? BusinessHours { get; set; }
    // Preferred dashboard/website language (FK -> Language; matched by Language.Code).
    // Defaults to the locale the user signed up in. NULL = not set.
    public byte? AccountLanguageId { get; set; }
    public Language? AccountLanguage { get; set; }
    public byte PlanId { get; set; } = 2;

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public short? RegionId { get; set; }
    public string? PostalCode { get; set; }
    public short? CountryCodeId { get; set; }

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    // The Gigahoo number assigned to this account (FK -> PhoneNumber). Replaces the old
    // denormalized PhoneNumberSid/ForwardingPhone copies of the PhoneNumber row's data.
    public Guid? AssignedPhoneNumberId { get; set; }
    public PhoneNumber? AssignedPhoneNumber { get; set; }
    public DateOnly? BillingPeriodStart { get; set; }
    public DateOnly? BillingPeriodEnd { get; set; }
    public int MinutesUsed { get; set; }
    public DateTime? LimitNotifiedAt { get; set; }

    // Per-account post-call notification preferences (owner gets a summary per call).
    public bool ShouldSendCallSummaryEmail { get; set; } = true;
    public bool ShouldSendCallSummarySms { get; set; } = true;

    // Per-account "Questions" — which details the agent collects (default on). Off = don't ask/collect, and hide downstream.
    public bool ShouldCollectName { get; set; } = true;
    public bool ShouldCollectPhone { get; set; } = true;
    public bool ShouldCollectAddress { get; set; } = true;
    public bool ShouldCollectEmergency { get; set; } = true;

    // AI voice agent settings: custom call greeting + selected voice (NULL = default).
    public string? GreetingMessage { get; set; }
    public int? AgentVoiceId { get; set; }      // FK -> AgentVoice
    public AgentVoice? AgentVoice { get; set; }

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
