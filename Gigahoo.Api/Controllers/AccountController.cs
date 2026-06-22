using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("api")]
public class AccountController(GigahooDbContext db) : ControllerBase
{
    private Guid GetUserId() => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create([FromBody] CreateAccountRequest request)
    {
        var userId = GetUserId();

        var existing = await db.Accounts.AnyAsync(a => a.UserId == userId);
        if (existing) return Conflict(new { error = "Account already exists" });

        var account = new Account
        {
            UserId = userId,
            BusinessName = request.BusinessName,
            CategoryId = request.CategoryId,
            BusinessPhone = request.BusinessPhone,
            PhoneCountryCode = request.PhoneCountryCode,
            Email = request.Email,
            PlanId = request.PlanId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        return Ok(await MapToResponse(account));
    }

    [HttpGet]
    public async Task<ActionResult<AccountResponse>> Get()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts
            .Include(a => a.Plan)
            .Include(a => a.Category)
            .Include(a => a.Country)
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
            .Include(a => a.Country)
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
        account.CountryId = request.CountryId;
        account.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Reload includes
        await db.Entry(account).Reference(a => a.Category).LoadAsync();
        await db.Entry(account).Reference(a => a.Country).LoadAsync();
        if (account.RegionId.HasValue)
            await db.Entry(account).Reference(a => a.Region).LoadAsync();

        return Ok(await MapToResponse(account));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account is null) return NotFound();

        // Delete the user (this will cascade delete the account due to foreign key)
        db.Users.Remove(account.User);
        await db.SaveChangesAsync();

        return Ok(new { message = "Account deleted successfully" });
    }

    private async Task<AccountResponse> MapToResponse(Account account)
    {
        var plan = account.Plan ?? await db.Plans.FindAsync(account.PlanId);
        var category = account.Category ?? await db.BusinessCategories.FindAsync(account.CategoryId);
        var country = account.Country ?? await db.Countries.FindAsync(account.CountryId);

        string? regionName = null;
        if (account.RegionId.HasValue)
        {
            var region = account.Region ?? await db.Regions.FindAsync(account.RegionId);
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
            account.BusinessName,
            category?.Name ?? "",
            account.CategoryId,
            account.BusinessPhone,
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
            account.CountryId,
            plan?.Name ?? "",
            account.PlanId,
            plan?.IncludedMinutes ?? 0,
            billingPeriod,
            account.MinutesUsed,
            account.CreatedAt
        );
    }
}
