using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/account/security")]
[Authorize]
public class AccountSecurityController(GigahooDbContext db) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpGet("linked-accounts")]
    public async Task<ActionResult<LinkedAccountsResponse>> GetLinkedAccounts()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FindAsync(accountId);

        if (account is null) return NotFound();

        return Ok(new LinkedAccountsResponse(
            account.GoogleSubjectId != null,
            account.Email,
            account.BusinessPhoneNumber
        ));
    }

    [HttpPost("link-google")]
    public async Task<IActionResult> LinkGoogle([FromBody] LinkGoogleRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FindAsync(accountId);

        if (account is null) return NotFound();

        if (account.GoogleSubjectId != null)
            return BadRequest(new { error = "Google account already linked" });

        account.GoogleSubjectId = request.GoogleSubjectId;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Google account linked successfully" });
    }

    [HttpPost("unlink-google")]
    public async Task<IActionResult> UnlinkGoogle()
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FindAsync(accountId);

        if (account is null) return NotFound();

        if (account.GoogleSubjectId == null)
            return BadRequest(new { error = "No Google account linked" });

        // The remaining login methods are password, magic-link email, or business-phone SMS.
        if (account.PasswordHash == null && account.Email == null && account.BusinessPhoneNumber == null)
            return BadRequest(new { error = "Cannot unlink Google: no other login method available" });

        account.GoogleSubjectId = null;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Google account unlinked successfully" });
    }

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FindAsync(accountId);

        if (account is null) return NotFound();

        var existing = await db.Accounts.FirstOrDefaultAsync(a => a.NormalizedEmail == request.NewEmail.ToLowerInvariant() && a.AccountId != accountId);
        if (existing != null)
            return BadRequest(new { error = "Email already in use" });

        account.Email = request.NewEmail;
        account.NormalizedEmail = request.NewEmail.ToLowerInvariant();
        account.IsEmailConfirmed = false;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Email changed successfully. Please verify your new email." });
    }

    [HttpPost("set-primary")]
    public async Task<IActionResult> SetPrimaryLoginMethod([FromBody] SetPrimaryRequest request)
    {
        var accountId = GetAccountId();
        var account = await db.Accounts.FindAsync(accountId);

        if (account is null) return NotFound();

        return Ok(new { message = $"Primary login method set to {request.Method}" });
    }
}

public record LinkedAccountsResponse(
    bool GoogleLinked,
    string Email,
    string? Phone
);

public record LinkGoogleRequest(string GoogleSubjectId);
public record ChangeEmailRequest(string NewEmail);
public record SetPrimaryRequest(string Method);
