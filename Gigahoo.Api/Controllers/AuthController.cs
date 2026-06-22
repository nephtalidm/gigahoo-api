using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    GigahooDbContext db,
    IJwtTokenService jwt,
    IGoogleAuthService googleAuth,
    IOtpService otp,
    IEmailService email,
    ISmsService sms,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleAuthRequest request)
    {
        var payload = await googleAuth.ValidateIdTokenAsync(request.IdToken);
        if (payload is null) return Unauthorized(new { error = "Invalid Google token" });

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == payload.Subject);
        var isNew = user is null;

        if (isNew)
        {
            user = new User
            {
                Email = payload.Email,
                NormalizedEmail = payload.Email.ToLowerInvariant(),
                GoogleSubjectId = payload.Subject,
                DisplayName = payload.Name,
                IsEmailConfirmed = payload.EmailVerified,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
        var accountId = account?.Id ?? Guid.Empty;

        var accessToken = jwt.GenerateAccessToken(user, accountId);
        var refreshToken = jwt.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(30), isNew));
    }

    [HttpPost("magic-link")]
    public async Task<IActionResult> SendMagicLink([FromBody] SendMagicLinkRequest request)
    {
        var code = await otp.GenerateAndStoreAsync(request.Email, "EmailMagicLink", TimeSpan.FromMinutes(15));
        var link = $"{Request.Scheme}://{Request.Host}/api/auth/verify-magic-link?email={Uri.EscapeDataString(request.Email)}&code={code}";

        await email.SendMagicLinkAsync(request.Email, link);
        logger.LogInformation("Magic link sent to {Email}", request.Email);

        return Ok(new { message = "If an account exists, a magic link has been sent." });
    }

    [HttpGet("verify-magic-link")]
    public async Task<ActionResult<AuthResponse>> VerifyMagicLink([FromQuery] string email, [FromQuery] string code)
    {
        var valid = await otp.VerifyAsync(email, "EmailMagicLink", code);
        if (!valid) return BadRequest(new { error = "Invalid or expired link" });

        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToLowerInvariant());
        var isNew = user is null;

        if (isNew)
        {
            user = new User
            {
                Email = email,
                NormalizedEmail = email.ToLowerInvariant(),
                IsEmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.IsEmailConfirmed = true;
        }

        await db.SaveChangesAsync();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
        var accountId = account?.Id ?? Guid.Empty;

        var accessToken = jwt.GenerateAccessToken(user, accountId);
        var refreshToken = jwt.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(30), isNew));
    }

    [HttpPost("sms/send")]
    public async Task<IActionResult> SendSmsCode([FromBody] SendSmsCodeRequest request)
    {
        var code = await otp.GenerateAndStoreAsync(request.PhoneNumber, "SmsVerification", TimeSpan.FromMinutes(10));
        await sms.SendVerificationCodeAsync(request.PhoneNumber, code);
        logger.LogInformation("SMS code sent to {Phone}", request.PhoneNumber);

        return Ok(new { message = "Verification code sent." });
    }

    [HttpPost("sms/verify")]
    public async Task<ActionResult<AuthResponse>> VerifySmsCode([FromBody] VerifySmsCodeRequest request)
    {
        var valid = await otp.VerifyAsync(request.PhoneNumber, "SmsVerification", request.Code);
        if (!valid) return BadRequest(new { error = "Invalid or expired code" });

        var normalizedPhone = request.PhoneNumber.Replace(" ", "").Replace("-", "");
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedPhone == normalizedPhone);
        var isNew = user is null;

        if (isNew)
        {
            user = new User
            {
                PhoneNumber = request.PhoneNumber,
                NormalizedPhone = normalizedPhone,
                IsPhoneConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.IsPhoneConfirmed = true;
        }

        await db.SaveChangesAsync();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
        var accountId = account?.Id ?? Guid.Empty;

        var accessToken = jwt.GenerateAccessToken(user, accountId);
        var refreshToken = jwt.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(30), isNew));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenRefreshResponse>> Refresh([FromBody] TokenRefreshRequest request)
    {
        var storedToken = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

        if (storedToken is null) return Unauthorized(new { error = "Invalid refresh token" });

        storedToken.RevokedAt = DateTime.UtcNow;

        var newRefreshToken = jwt.GenerateRefreshToken();
        storedToken.ReplacedByToken = newRefreshToken;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = storedToken.UserId,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == storedToken.UserId);
        var accountId = account?.Id ?? Guid.Empty;

        var accessToken = jwt.GenerateAccessToken(storedToken.User, accountId);

        return Ok(new TokenRefreshResponse(accessToken, newRefreshToken, DateTime.UtcNow.AddMinutes(30)));
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke([FromBody] TokenRefreshRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Unauthorized();

        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == Guid.Parse(userId));

        if (token is not null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok();
    }
}
