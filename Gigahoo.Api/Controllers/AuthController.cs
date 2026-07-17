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
        var isNew = false;

        if (account is null)
        {
            // Link to an existing account that already owns this email (e.g. one
            // created via email magic-link) instead of creating a duplicate.
            // Only auto-link when Google has verified the address — linking on an
            // unverified email would be an account-takeover risk.
            if (payload.EmailVerified)
                account = await db.Accounts.FirstOrDefaultAsync(a => a.NormalizedEmail == payload.Email.ToLowerInvariant());

            if (account is not null)
            {
                account.GoogleSubjectId = payload.Subject;
                account.IsEmailConfirmed = true;
                account.LastLoginAt = DateTime.UtcNow;
            }
            else
            {
                // NEW signup — enforce the region gate BEFORE creating the account, so an
                // unsupported-market user never gets a persisted account (a later Google login
                // would otherwise find it "existing" and skip the gate, letting them straight in).
                if (!string.IsNullOrEmpty(request.Country) &&
                    !await db.Countries.AnyAsync(c => c.Code == request.Country && c.IsSupported))
                {
                    return StatusCode(403, new { error = "region_signup_restricted" });
                }

                isNew = true;
                account = new Account
                {
                    Email = payload.Email,
                    NormalizedEmail = payload.Email.ToLowerInvariant(),
                    GoogleSubjectId = payload.Subject,
                    IsEmailConfirmed = payload.EmailVerified,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                };
                db.Accounts.Add(account);
            }
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
        // Existing account => sign-in copy; otherwise sign-up copy.
        var exists = await db.Accounts.AnyAsync(a => a.NormalizedEmail == request.Email.ToLowerInvariant());

        // Block NEW-account signups from non-supported / coming-soon markets. Existing
        // accounts (login) always proceed.
        if (!string.IsNullOrEmpty(request.Country) && !exists &&
            !await db.Countries.AnyAsync(c => c.Code == request.Country && c.IsSupported))
        {
            return StatusCode(403, new { error = "region_signup_restricted" });
        }

        // Rate-limit: at most one verification code per minute per email.
        var emailWait = await otp.SecondsUntilResendAsync(request.Email, "EmailMagicLink", TimeSpan.FromMinutes(1));
        if (emailWait > 0)
            return StatusCode(429, new { error = $"Please wait {emailWait} seconds before requesting another code." });

        var code = await otp.GenerateAndStoreAsync(request.Email, "EmailMagicLink", TimeSpan.FromMinutes(15));
        var frontendUrl = config["Frontend:Url"] ?? "http://localhost:3000";
        var link = $"{frontendUrl}/auth/callback?email={Uri.EscapeDataString(request.Email)}&code={code}";

        await email.SendMagicLinkAsync(request.Email, link, exists ? VerificationPurpose.SignIn : VerificationPurpose.SignUp);
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

        // A valid magic link proves the user owns this email, so an existing
        // account (created via email, Google, or otherwise) is simply logged in —
        // making email and Google interchangeable for the same verified address.
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
        // Whether an account already owns this phone (same business-phone matching rule
        // that sms/verify uses).
        var exists = await FindAccountByPhoneAsync(request.PhoneNumber) is not null;

        // Block NEW-account signups from non-supported / coming-soon markets. Existing
        // accounts (login) always proceed.
        if (!string.IsNullOrEmpty(request.Country) && !exists &&
            !await db.Countries.AnyAsync(c => c.Code == request.Country && c.IsSupported))
        {
            return StatusCode(403, new { error = "region_signup_restricted" });
        }

        // Rate-limit: at most one verification code per minute per phone number.
        var smsWait = await otp.SecondsUntilResendAsync(request.PhoneNumber, "SmsVerification", TimeSpan.FromMinutes(1));
        if (smsWait > 0)
            return StatusCode(429, new { error = $"Please wait {smsWait} seconds before requesting another code." });

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

        var account = await FindAccountByPhoneAsync(request.PhoneNumber);
        var isNew = account is null;

        if (isNew)
        {
            account = new Account
            {
                // The number they authenticated with becomes the business phone — the ONE
                // phone identity an account has (used for future SMS logins too).
                BusinessPhoneNumber = request.PhoneNumber,
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
    // The ONE phone-matching rule for SMS auth: an account is identified by its BUSINESS
    // phone number - its local digits plus its country's dial code - compared as full
    // E.164 digits, so the same local number in two countries can never collide.
    private async Task<Account?> FindAccountByPhoneAsync(string phoneNumber)
    {
        var incomingDigits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (incomingDigits.Length < 8) return null;
        var dialByCountry = await db.Countries
            .ToDictionaryAsync(c => c.Code, c => new string(c.DialCode.Where(char.IsDigit).ToArray()));
        var candidates = await db.Accounts.Where(a => a.BusinessPhoneNumber != null).ToListAsync();
        return candidates.FirstOrDefault(a =>
        {
            var bizDigits = new string(a.BusinessPhoneNumber!.Where(char.IsDigit).ToArray());
            var dial = dialByCountry.TryGetValue(a.PhoneCountryCode, out var d) ? d : "";
            // The business phone may be stored with or without the country code.
            return incomingDigits == dial + bizDigits || incomingDigits == bizDigits;
        });
    }
}
