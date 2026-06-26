using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
using Gigahoo.Api.Services.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("api")]
public class AccountController(
    GigahooDbContext db,
    IJwtTokenService jwt,
    IGoogleAuthService googleAuth,
    IStripeService stripe,
    ITwilioService twilio,
    ITelephonyProvider telephony,
    IEmailService email,
    ISmsProvider sms,
    IOtpService otp,
    IConfiguration config,
    ILogger<AccountController> logger) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create([FromBody] CreateAccountRequest request)
    {
        var accountId = GetAccountId();

        var account = await db.Accounts
            .Include(a => a.Plan)
            .Include(a => a.Category)
            .Include(a => a.Region)
            .FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        // Check email uniqueness (only if email changed)
        if (account.NormalizedEmail != request.Email.ToLowerInvariant())
        {
            var emailTaken = await db.Accounts.AnyAsync(a => a.NormalizedEmail == request.Email.ToLowerInvariant() && a.Id != accountId);
            if (emailTaken) return Conflict(new { error = "This email is already registered" });
        }

        // Check phone uniqueness
        var normalizedPhone = request.BusinessPhone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        var phoneTaken = await db.Accounts.AnyAsync(a => a.BusinessPhone == request.BusinessPhone && a.Id != accountId);
        if (phoneTaken) return Conflict(new { error = "This phone number is already in use" });

        // US and Canada share the +1 country code, so the area code (first 3
        // digits) is the only thing that distinguishes them. Reject a phone whose
        // NANP area code doesn't belong to the selected phone country. This is the
        // authoritative gate — it rejects even if the frontend check is bypassed.
        if (!NanpAreaCodes.MatchesCountry(request.BusinessPhone, request.PhoneCountryCode))
            return BadRequest(new { error = "This phone number's area code doesn't match the selected country." });

        // Password is required only for plain-email signups. SMS and Google are
        // passwordless (a password can be added later in Settings).
        var isEmailMethod = account.GoogleSubjectId is null && !account.IsPhoneConfirmed;
        if (string.IsNullOrEmpty(request.Password))
        {
            if (isEmailMethod && account.PasswordHash is null)
                return BadRequest(new { error = "Password is required" });
        }
        else
        {
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        account.BusinessName = request.BusinessName;
        account.CategoryId = request.CategoryId;
        account.BusinessPhone = request.BusinessPhone;
        account.PhoneCountryCode = request.PhoneCountryCode;
        account.Email = request.Email;
        account.NormalizedEmail = request.Email.ToLowerInvariant();
        account.PlanId = request.PlanId;
        account.AddressLine1 = request.AddressLine1;
        account.AddressLine2 = request.AddressLine2;
        account.City = request.City;
        account.RegionCustom = request.Region;
        account.PostalCode = request.PostalCode;
        // Gigahoo only serves countries flagged IsSupported in the Country table —
        // reject any other business country before saving.
        var supported = await db.Countries.AnyAsync(c => c.Code == request.CountryCode && c.IsSupported);
        if (!supported)
            return BadRequest(new { error = "Gigahoo is currently available only in the US and Canada." });
        // Resolve the business country (ISO-2) to its Country id. Leave null if
        // unknown — never fail signup over an unrecognized code.
        account.CountryCodeId = (await db.Countries.FirstOrDefaultAsync(c => c.Code == request.CountryCode))?.Id;
        account.UpdatedAt = DateTime.UtcNow;
        // NOTE: deliberately NOT saving the account yet. For free signups we persist
        // the account only once a phone number has actually been provisioned.

        var plan = await db.Plans.FindAsync(account.PlanId);

        if (plan is not null && plan.PriceMonthly == 0)
        {
            // FREE plan: provision a number FIRST. If none can be provisioned we alert
            // admin, delete the (auth-created) account, and fail the signup — no
            // account record is kept for a customer we can't give a number.
            account.BillingPeriodStart = DateOnly.FromDateTime(DateTime.UtcNow);
            account.BillingPeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
            var countryCode = account.CountryCodeId is short ccid && await db.Countries.FindAsync(ccid) is { } co
                ? co.Code : account.PhoneCountryCode;
            try
            {
                var number = await twilio.GetAvailableNumberAsync(countryCode);
                if (number is null)
                {
                    var purchased = await twilio.PurchasePhoneNumberAsync(countryCode);
                    if (purchased is not null)
                    {
                        number = new PhoneNumber
                        {
                            Sid = purchased.Sid, Number = purchased.PhoneNumber, CountryCode = countryCode,
                            Provider = telephony.ProviderName, Status = PhoneNumberStatus.Available,
                            MonthlyCost = 1.15m, PurchasedAt = DateTime.UtcNow
                        };
                        db.PhoneNumbers.Add(number);
                        await db.SaveChangesAsync();
                    }
                }
                if (number is not null)
                {
                    await twilio.AssignNumberToAccountAsync(number, account.Id);
                    account.PhoneNumberSid = number.Sid;
                    account.TelephonyProvider = number.Provider;
                    account.ForwardingPhone = number.Number;
                    await twilio.ConfigureWebhookAsync(number.Sid, $"{config["VoiceAgent:PublicUrl"]}/twilio/voice?accountId={account.Id}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Free plan number provisioning failed for account {Account}", account.Id);
            }

            if (string.IsNullOrEmpty(account.PhoneNumberSid))
            {
                try
                {
                    await email.SendAdminAlertAsync(
                        "Signup failed — no phone number available",
                        $"Could not provision a phone number for a new free-plan signup.\n\nAccount: {account.Id}\nBusiness: {account.BusinessName}\nEmail: {account.Email}\nCountry: {countryCode}\nWhen: {DateTime.UtcNow:u}");
                }
                catch (Exception ex) { logger.LogError(ex, "Failed to send admin alert for provisioning failure {Account}", account.Id); }

                // No number — delete the auth-created account; keep no record.
                db.Accounts.Remove(account);
                await db.SaveChangesAsync();

                return BadRequest(new { error = "We couldn't set up a phone number for your account right now. Our team has been notified — please try again shortly or contact contact@gigahoo.ai." });
            }

            // Number provisioned — persist the account + assignment, then welcome.
            await db.SaveChangesAsync();

            try { await email.SendPhoneNumberAssignedAsync(account.Email!, account.BusinessName, account.ForwardingPhone!); }
            catch (Exception ex) { logger.LogError(ex, "Free welcome email failed for account {Account}", account.Id); }
            var ownerPhone = account.PhoneNumber ?? account.BusinessPhone;
            if (!string.IsNullOrEmpty(ownerPhone))
            {
                try { await sms.SendAsync(ownerPhone, $"Welcome to Gigahoo!\n\nHi {account.BusinessName}, your dedicated phone number is ready to receive calls:\n{account.ForwardingPhone}\n\nNext steps:\n1. Forward your existing business calls to this number\n2. Test the AI receptionist by calling the number yourself\n3. Configure your business details in the dashboard\n\nNeed help? Contact us at contact@gigahoo.ai"); }
                catch (Exception ex) { logger.LogError(ex, "Free welcome SMS failed for account {Account}", account.Id); }
            }
        }
        else
        {
            // PAID plan: create the Stripe customer (best-effort), then persist. The
            // phone number is provisioned at checkout / the invoice.paid webhook.
            if (plan is not null && string.IsNullOrEmpty(account.StripeCustomerId))
            {
                try { account.StripeCustomerId = await stripe.CreateCustomerAsync(account.Email!, account.BusinessName); }
                catch (Exception ex) { logger.LogError(ex, "Failed to create Stripe customer at signup for account {Account}", account.Id); }
            }
            await db.SaveChangesAsync();
        }

        var token = jwt.GenerateAccessToken(account);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        return Ok(new { token, expiresAt, account = await MapToResponse(account) });
    }

    [HttpGet]
    public async Task<ActionResult<AccountResponse>> Get()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts
            .Include(a => a.Plan)
            .Include(a => a.Category)
            .Include(a => a.Region)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account is null) return NotFound();
        return Ok(await MapToResponse(account));
    }

    [HttpPut]
    public async Task<ActionResult<AccountResponse>> Update([FromBody] UpdateAccountRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts
            .Include(a => a.Plan)
            .Include(a => a.Category)
            .Include(a => a.Region)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account is null) return NotFound();

        // Enforce email uniqueness across all accounts (any signup method).
        if (account.NormalizedEmail != request.Email.ToLowerInvariant())
        {
            var emailTaken = await db.Accounts.AnyAsync(a => a.NormalizedEmail == request.Email.ToLowerInvariant() && a.Id != accountId);
            if (emailTaken) return Conflict(new { error = "This email is already registered" });
        }

        account.BusinessName = request.BusinessName;
        account.CategoryId = request.CategoryId;
        account.BusinessPhone = request.BusinessPhone;
        account.PhoneCountryCode = request.PhoneCountryCode;
        account.Email = request.Email;
        account.NormalizedEmail = request.Email.ToLowerInvariant();
        account.WebsiteUrl = request.WebsiteUrl;
        account.ServiceArea = request.ServiceArea;
        account.BusinessHours = request.BusinessHours;
        account.AddressLine1 = request.AddressLine1;
        account.AddressLine2 = request.AddressLine2;
        account.City = request.City;
        account.RegionId = request.RegionId;
        account.RegionCustom = request.RegionCustom;
        account.PostalCode = request.PostalCode;
        account.CountryCodeId = request.CountryId;
        account.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await db.Entry(account).Reference(a => a.Category).LoadAsync();
        if (account.RegionId.HasValue)
            await db.Entry(account).Reference(a => a.Region).LoadAsync();

        return Ok(await MapToResponse(account));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FindAsync(accountId);

        if (account is null) return NotFound();

        db.Accounts.Remove(account);
        await db.SaveChangesAsync();

        return Ok(new { message = "Account deleted successfully" });
    }

    [HttpPost("password")]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        // Require the current password only when the password is the account's sole
        // credential. Accounts that can also sign in via Google or SMS can always
        // re-authenticate, so they may (re)set a password without the old one.
        var hasAlternateAuth = account.GoogleSubjectId is not null || account.IsPhoneConfirmed;
        if (account.PasswordHash is not null && !hasAlternateAuth)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) ||
                !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, account.PasswordHash))
                return BadRequest(new { error = "Current password is incorrect" });
        }

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Password updated" });
    }

    [HttpPost("link-google")]
    public async Task<IActionResult> LinkGoogle([FromBody] GoogleAuthRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        var payload = await googleAuth.ValidateIdTokenAsync(request.IdToken);
        if (payload is null) return Unauthorized(new { error = "Invalid Google token" });
        if (!payload.EmailVerified) return BadRequest(new { error = "Google email is not verified" });

        // Don't steal a Google identity already linked to a different account.
        var inUse = await db.Accounts.AnyAsync(a => a.GoogleSubjectId == payload.Subject && a.Id != accountId);
        if (inUse) return Conflict(new { error = "This Google account is already linked to another account" });

        account.GoogleSubjectId = payload.Subject;
        account.DisplayName ??= payload.Name;
        account.IsEmailConfirmed = true;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Google linked" });
    }

    [HttpPost("email/request-change")]
    public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        var newEmail = request.NewEmail.Trim();
        if (string.IsNullOrEmpty(newEmail) || !newEmail.Contains('@'))
            return BadRequest(new { error = "Enter a valid email address" });

        var normalized = newEmail.ToLowerInvariant();
        if (normalized == account.NormalizedEmail)
            return BadRequest(new { error = "This is already your email address" });

        var taken = await db.Accounts.AnyAsync(a => a.NormalizedEmail == normalized && a.Id != accountId);
        if (taken) return Conflict(new { error = "This email is already registered" });

        var code = await otp.GenerateAndStoreAsync(newEmail, "EmailChange", TimeSpan.FromMinutes(15));
        await email.SendEmailChangeCodeAsync(newEmail, code);
        logger.LogInformation("Email change code sent for account {Account}", accountId);

        return Ok(new { message = "Verification code sent." });
    }

    [HttpPost("email/confirm-change")]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        var newEmail = request.NewEmail.Trim();
        var normalized = newEmail.ToLowerInvariant();
        var taken = await db.Accounts.AnyAsync(a => a.NormalizedEmail == normalized && a.Id != accountId);
        if (taken) return Conflict(new { error = "This email is already registered" });

        var valid = await otp.VerifyAsync(newEmail, "EmailChange", request.Code);
        if (!valid) return BadRequest(new { error = "Invalid or expired code" });

        account.Email = newEmail;
        account.NormalizedEmail = normalized;
        account.IsEmailConfirmed = true;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Email updated" });
    }

    [HttpPost("phone/request-change")]
    public async Task<IActionResult> RequestPhoneChange([FromBody] RequestPhoneChangeRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        var newPhone = request.NewPhone.Trim();
        if (string.IsNullOrEmpty(newPhone))
            return BadRequest(new { error = "Enter a valid phone number" });

        // US/CA share +1; only the NANP area code distinguishes them. Reject a new
        // phone whose area code doesn't match the account's stored phone country.
        if (!NanpAreaCodes.MatchesCountry(newPhone, account.PhoneCountryCode))
            return BadRequest(new { error = "This phone number's area code doesn't match the selected country." });

        var code = await otp.GenerateAndStoreAsync(newPhone, "PhoneChange", TimeSpan.FromMinutes(10));
        await sms.SendAsync(newPhone, $"Your Gigahoo verification code is {code}");
        logger.LogInformation("Phone change code sent for account {Account}", accountId);

        return Ok(new { message = "Verification code sent." });
    }

    [HttpPost("phone/confirm-change")]
    public async Task<IActionResult> ConfirmPhoneChange([FromBody] ConfirmPhoneChangeRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        // Authoritative area-code gate: re-check before applying the change, using
        // the account's stored phone country (US/CA disambiguation under +1).
        if (!NanpAreaCodes.MatchesCountry(request.NewPhone, account.PhoneCountryCode))
            return BadRequest(new { error = "This phone number's area code doesn't match the selected country." });

        var valid = await otp.VerifyAsync(request.NewPhone, "PhoneChange", request.Code);
        if (!valid) return BadRequest(new { error = "Invalid or expired code" });

        account.BusinessPhone = request.NewPhone.Trim();
        if (!string.IsNullOrEmpty(request.PhoneCountryCode))
            account.PhoneCountryCode = request.PhoneCountryCode;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Phone updated" });
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<CallNotificationsResponse>> GetNotifications()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        return Ok(new CallNotificationsResponse(account.EmailCallNotifications, account.SmsCallNotifications));
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] UpdateCallNotificationsRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound();

        account.EmailCallNotifications = request.EmailCallNotifications;
        account.SmsCallNotifications = request.SmsCallNotifications;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new CallNotificationsResponse(account.EmailCallNotifications, account.SmsCallNotifications));
    }

    private async Task<AccountResponse> MapToResponse(Account account)
    {
        var plan = account.Plan ?? await db.Plans.FindAsync(account.PlanId);
        var category = account.Category ?? await db.BusinessCategories.FindAsync(account.CategoryId);
        var country = await db.Countries.FindAsync(account.CountryCodeId);

        string? regionName = null;
        if (account.RegionId.HasValue)
        {
            var region = await db.Regions.FindAsync(account.RegionId);
            regionName = region?.Name ?? account.RegionCustom;
        }
        else
        {
            regionName = account.RegionCustom;
        }

        var billingPeriod = account.BillingPeriodStart.HasValue && account.BillingPeriodEnd.HasValue
            ? $"{account.BillingPeriodStart:MMM d} - {account.BillingPeriodEnd:MMM d}"
            : "";

        return new AccountResponse(
            account.Id,
            account.BusinessName ?? "",
            category?.Name ?? "",
            account.CategoryId ?? 0,
            account.BusinessPhone ?? "",
            account.PhoneCountryCode,
            account.Email,
            account.ServiceArea,
            account.WebsiteUrl,
            account.BusinessHours,
            account.ForwardingPhone,
            account.AddressLine1,
            account.AddressLine2,
            account.City,
            regionName,
            account.RegionId,
            account.PostalCode,
            country?.Name ?? "",
            (short)(account.CountryCodeId ?? 0),
            plan?.Name ?? "",
            account.PlanId,
            plan?.IncludedMinutes ?? 0,
            billingPeriod,
            account.MinutesUsed,
            account.CreatedAt,
            account.PasswordHash is not null,
            account.GoogleSubjectId is not null,
            account.PasswordHash is not null && account.GoogleSubjectId is null && !account.IsPhoneConfirmed,
            account.EmailCallNotifications,
            account.SmsCallNotifications
        );
    }
}
