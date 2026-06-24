using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(
    GigahooDbContext db,
    IJwtTokenService jwt,
    IGoogleAuthService googleAuth,
    IOtpService otp,
    IEmailService email,
    ISmsService sms,
    IConfiguration config,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleAuthRequest request)
    {
        var payload = await googleAuth.ValidateIdTokenAsync(request.IdToken);
        if (payload is null) return Unauthorized(new { error = "Invalid Google token" });

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.GoogleSubjectId == payload.Subject);
        var isNew = account is null;

        if (isNew)
        {
            account = new Account
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
            db.Accounts.Add(account);
        }
        else
        {
            account.LastLoginAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var accessToken = jwt.GenerateAccessToken(account);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        return Ok(new AuthResponse(accessToken, expiresAt, isNew));
    }

    [HttpPost("magic-link")]
    public async Task<IActionResult> SendMagicLink([FromBody] SendMagicLinkRequest request)
    {
        var code = await otp.GenerateAndStoreAsync(request.Email, "EmailMagicLink", TimeSpan.FromMinutes(15));
        var frontendUrl = config["Frontend:Url"] ?? "http://localhost:3000";
        var link = $"{frontendUrl}/auth/callback?email={Uri.EscapeDataString(request.Email)}&code={code}";

        await email.SendMagicLinkAsync(request.Email, link);
        logger.LogInformation("Magic link sent to {Email}", request.Email);

        return Ok(new { message = "If an account exists, a magic link has been sent." });
    }

    [HttpPost("check-email")]
    public async Task<ActionResult> CheckEmail([FromBody] SendMagicLinkRequest request)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.NormalizedEmail == request.Email.ToLowerInvariant());
        if (account is null)
            return Ok(new { exists = false });
        return Ok(new { exists = true, hasPassword = !string.IsNullOrEmpty(account.PasswordHash) });
    }

    [HttpPost("login-password")]
    public async Task<ActionResult<AuthResponse>> LoginPassword([FromBody] LoginPasswordRequest request)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.NormalizedEmail == request.Email.ToLowerInvariant());
        if (account is null || string.IsNullOrEmpty(account.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password" });

        if (!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password" });

        account.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var accessToken = jwt.GenerateAccessToken(account);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        return Ok(new AuthResponse(accessToken, expiresAt, false));
    }

    [HttpPost("verify-magic-link")]
    public async Task<ActionResult<AuthResponse>> VerifyMagicLink([FromBody] VerifyMagicLinkRequest request)
    {
        var valid = await otp.VerifyAsync(request.Email, "EmailMagicLink", request.Code);
        if (!valid) return BadRequest(new { error = "Invalid or expired link" });

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.NormalizedEmail == request.Email.ToLowerInvariant());

        if (account is not null && account.IsEmailConfirmed)
            return BadRequest(new { error = "Email already verified. Please sign in instead." });

        var isNew = account is null;

        if (isNew)
        {
            account = new Account
            {
                Email = request.Email,
                NormalizedEmail = request.Email.ToLowerInvariant(),
                IsEmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            db.Accounts.Add(account);
        }
        else
        {
            account.IsEmailConfirmed = true;
            account.LastLoginAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var accessToken = jwt.GenerateAccessToken(account);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        return Ok(new AuthResponse(accessToken, expiresAt, isNew));
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
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.NormalizedPhone == normalizedPhone);
        var isNew = account is null;

        if (isNew)
        {
            account = new Account
            {
                PhoneNumber = request.PhoneNumber,
                NormalizedPhone = normalizedPhone,
                IsPhoneConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            db.Accounts.Add(account);
        }
        else
        {
            account.LastLoginAt = DateTime.UtcNow;
            account.IsPhoneConfirmed = true;
        }

        await db.SaveChangesAsync();

        var accessToken = jwt.GenerateAccessToken(account);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        return Ok(new AuthResponse(accessToken, expiresAt, isNew));
    }
}
