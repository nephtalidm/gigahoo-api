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
    IJwtTokenService jwt) : ControllerBase
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

        account.BusinessName = request.BusinessName;
        account.CategoryId = request.CategoryId;
        account.BusinessPhone = request.BusinessPhone;
        account.PhoneCountryCode = request.PhoneCountryCode;
        account.Email = request.Email;
        account.NormalizedEmail = request.Email.ToLowerInvariant();
        account.PlanId = request.PlanId;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
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

        account.BusinessName = request.BusinessName;
        account.CategoryId = request.CategoryId;
        account.BusinessPhone = request.BusinessPhone;
        account.PhoneCountryCode = request.PhoneCountryCode;
        account.Email = request.Email;
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
            account.CreatedAt
        );
    }
}
