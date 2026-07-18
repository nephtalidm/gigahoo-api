using System.ComponentModel.DataAnnotations;

namespace Gigahoo.Api.Dtos;

public record SendMagicLinkRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    // Where to resume after login (e.g. /dashboard/billing from a receipt email's button).
    // Embedded into the magic link; validated server-side to dashboard paths only.
    [MaxLength(300)]
    public string? Next { get; init; }

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

    [Required, MinLength(6), MaxLength(6)]
    public string Code { get; init; } = default!;
}

public record GoogleAuthRequest
{
    [Required, MaxLength(4096)]
    public string IdToken { get; init; } = default!;

    // The visitor's market (ISO-2), used to gate NEW-account signups from non-supported
    // regions — mirrors the magic-link / SMS flows. Optional; existing accounts always log in.
    [MaxLength(2)]
    public string? Country { get; init; }
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
