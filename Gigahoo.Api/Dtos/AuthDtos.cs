using System.ComponentModel.DataAnnotations;

namespace Gigahoo.Api.Dtos;

public record SendMagicLinkRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    // Selected market (ISO-2). Used to gate new-account signups from non-supported regions.
    [MaxLength(2)]
    public string? Country { get; init; }
}

public record SendSmsCodeRequest
{
    [Required, MaxLength(20)]
    public string PhoneNumber { get; init; } = default!;

    // Selected market (ISO-2). Used to gate new-account signups from non-supported regions.
    [MaxLength(2)]
    public string? Country { get; init; }
}

public record VerifySmsCodeRequest
{
    [Required, MaxLength(20)]
    public string PhoneNumber { get; init; } = default!;

    [Required, MaxLength(6), MinLength(4)]
    public string Code { get; init; } = default!;
}

public record GoogleAuthRequest
{
    [Required, MaxLength(4096)]
    public string IdToken { get; init; } = default!;
}

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    bool IsNewUser
);

public record VerifyMagicLinkRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    [Required, MaxLength(64)]
    public string Code { get; init; } = default!;
}

public record LoginPasswordRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    [Required, MaxLength(128)]
    public string Password { get; init; } = default!;
}
