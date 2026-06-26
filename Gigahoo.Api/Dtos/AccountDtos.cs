using System.ComponentModel.DataAnnotations;

namespace Gigahoo.Api.Dtos;

public record CreateAccountRequest
{
    [Required, MaxLength(200)]
    public string BusinessName { get; init; } = default!;

    [Required]
    public byte CategoryId { get; init; }

    [Required, MaxLength(30)]
    public string BusinessPhone { get; init; } = default!;

    [Required, MaxLength(5)]
    public string PhoneCountryCode { get; init; } = default!;

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    [Required]
    public byte PlanId { get; init; }

    [Required, MaxLength(200)]
    public string AddressLine1 { get; init; } = default!;

    [MaxLength(200)]
    public string? AddressLine2 { get; init; }

    [Required, MaxLength(100)]
    public string City { get; init; } = default!;

    // Free-text state/province/region; maps to Account.RegionCustom.
    [Required, MaxLength(100)]
    public string Region { get; init; } = default!;

    [Required, MaxLength(20)]
    public string PostalCode { get; init; } = default!;

    // ISO-2 of the business/address country; resolved to Account.CountryCodeId.
    [Required, MaxLength(2)]
    public string CountryCode { get; init; } = default!;

    // Optional: Google (OAuth) accounts don't need a password. Required for
    // email/SMS signups (enforced in the controller).
    [MinLength(8), MaxLength(128)]
    public string? Password { get; init; }
}

public record UpdateAccountRequest
{
    [Required, MaxLength(200)]
    public string BusinessName { get; init; } = default!;

    [Required]
    public byte CategoryId { get; init; }

    [Required, MaxLength(30)]
    public string BusinessPhone { get; init; } = default!;

    [Required, MaxLength(5)]
    public string PhoneCountryCode { get; init; } = default!;

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    [Url, MaxLength(500)]
    public string? WebsiteUrl { get; init; }

    [MaxLength(200)]
    public string? ServiceArea { get; init; }

    [MaxLength(200)]
    public string? BusinessHours { get; init; }

    [MaxLength(200)]
    public string? AddressLine1 { get; init; }

    [MaxLength(200)]
    public string? AddressLine2 { get; init; }

    [MaxLength(100)]
    public string? City { get; init; }

    public short? RegionId { get; init; }

    [MaxLength(100)]
    public string? RegionCustom { get; init; }

    [MaxLength(20)]
    public string? PostalCode { get; init; }

    [Required]
    public short CountryId { get; init; }
}

public record AccountResponse(
    Guid Id,
    string BusinessName,
    string Category,
    byte CategoryId,
    string BusinessPhone,
    string PhoneCountryCode,
    string Email,
    string? ServiceArea,
    string? WebsiteUrl,
    string? BusinessHours,
    string? ForwardingPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    short? RegionId,
    string? PostalCode,
    string Country,
    short CountryId,
    string Plan,
    byte PlanId,
    int IncludedMinutes,
    string BillingPeriod,
    int MinutesUsed,
    DateTime CreatedAt,
    bool HasPassword,
    bool HasGoogle,
    bool RequiresCurrentPassword
);

public record SetPasswordRequest
{
    public string? CurrentPassword { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; init; } = default!;
}

public record RequestEmailChangeRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string NewEmail { get; init; } = default!;
}

public record ConfirmEmailChangeRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string NewEmail { get; init; } = default!;

    [Required, MaxLength(10)]
    public string Code { get; init; } = default!;
}

public record RequestPhoneChangeRequest
{
    [Required, MaxLength(30)]
    public string NewPhone { get; init; } = default!;
}

public record ConfirmPhoneChangeRequest
{
    [Required, MaxLength(30)]
    public string NewPhone { get; init; } = default!;

    [MaxLength(5)]
    public string? PhoneCountryCode { get; init; }

    [Required, MaxLength(10)]
    public string Code { get; init; } = default!;
}
