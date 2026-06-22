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
    string? PostalCode,
    string Country,
    short CountryId,
    string Plan,
    byte PlanId,
    int IncludedMinutes,
    string BillingPeriod,
    int MinutesUsed,
    DateTime CreatedAt
);
