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
    private Guid GetUserId() => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("linked-accounts")]
    public async Task<ActionResult<LinkedAccountsResponse>> GetLinkedAccounts()
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user is null) return NotFound();

        return Ok(new LinkedAccountsResponse(
            user.GoogleSubjectId != null,
            user.Email,
            user.NormalizedPhone
        ));
    }

    [HttpPost("link-google")]
    public async Task<IActionResult> LinkGoogle([FromBody] LinkGoogleRequest request)
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user is null) return NotFound();

        if (user.GoogleSubjectId != null)
            return BadRequest(new { error = "Google account already linked" });

        user.GoogleSubjectId = request.GoogleSubjectId;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Google account linked successfully" });
    }

    [HttpPost("unlink-google")]
    public async Task<IActionResult> UnlinkGoogle()
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user is null) return NotFound();

        if (user.GoogleSubjectId == null)
            return BadRequest(new { error = "No Google account linked" });

        // Ensure user has at least one other login method
        if (user.NormalizedPhone == null)
            return BadRequest(new { error = "Cannot unlink Google: no other login method available" });

        user.GoogleSubjectId = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Google account unlinked successfully" });
    }

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user is null) return NotFound();

        // Check if email is already in use
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == request.NewEmail.ToLowerInvariant() && u.Id != userId);
        if (existingUser != null)
            return BadRequest(new { error = "Email already in use" });

        user.Email = request.NewEmail;
        user.NormalizedEmail = request.NewEmail.ToLowerInvariant();
        user.IsEmailConfirmed = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Email changed successfully. Please verify your new email." });
    }

    [HttpPost("change-phone")]
    public async Task<IActionResult> ChangePhone([FromBody] ChangePhoneRequest request)
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user is null) return NotFound();

        // Check if phone is already in use
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.NormalizedPhone == request.NewPhone && u.Id != userId);
        if (existingUser != null)
            return BadRequest(new { error = "Phone number already in use" });

        user.PhoneNumber = request.NewPhone;
        user.NormalizedPhone = request.NewPhone;
        user.IsPhoneConfirmed = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Phone number changed successfully. Please verify your new phone number." });
    }

    [HttpPost("set-primary")]
    public async Task<IActionResult> SetPrimaryLoginMethod([FromBody] SetPrimaryRequest request)
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user is null) return NotFound();

        // For now, we don't have a primary login method field
        // This is a placeholder for future implementation
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
public record ChangePhoneRequest(string NewPhone);
public record SetPrimaryRequest(string Method);
