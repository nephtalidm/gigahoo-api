namespace Gigahoo.Api.Dtos;

public record CreateAccountRequest(
    string BusinessName,
    byte CategoryId,
    string BusinessPhone,
    string PhoneCountryCode,
    string Email,
    byte PlanId
);

public record UpdateAccountRequest(
    string BusinessName,
    byte CategoryId,
    string BusinessPhone,
    string PhoneCountryCode,
    string Email,
    string? WebsiteUrl,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    short? RegionId,
    string? RegionCustom,
    string? PostalCode,
    short CountryId
);

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
