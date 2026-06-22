namespace Gigahoo.Api.Dtos;

public record SendMagicLinkRequest(string Email);

public record SendSmsCodeRequest(string PhoneNumber);

public record VerifySmsCodeRequest(string PhoneNumber, string Code);

public record GoogleAuthRequest(string IdToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    bool IsNewUser
);

public record TokenRefreshRequest(string RefreshToken);

public record TokenRefreshResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);
