using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
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
    IGoogleAuthService googleAuth) : ControllerBase
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
        account.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

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

        // Changing an existing password requires the current one. Setting a
        // password for the first time (e.g. a Google account) does not.
        if (account.PasswordHash is not null)
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
            account.GoogleSubjectId is not null
        );
    }
}
