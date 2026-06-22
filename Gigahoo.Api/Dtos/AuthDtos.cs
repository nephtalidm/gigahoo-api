using System.ComponentModel.DataAnnotations;

namespace Gigahoo.Api.Dtos;

public record SendMagicLinkRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;
}

public record SendSmsCodeRequest
{
    [Required, MaxLength(20)]
    public string PhoneNumber { get; init; } = default!;
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
    string RefreshToken,
    DateTime ExpiresAt,
    bool IsNewUser
);

public record TokenRefreshRequest
{
    [Required, MaxLength(512)]
    public string RefreshToken { get; init; } = default!;
}

public record TokenRefreshResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

public record VerifyMagicLinkRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    [Required, MaxLength(64)]
    public string Code { get; init; } = default!;
}
