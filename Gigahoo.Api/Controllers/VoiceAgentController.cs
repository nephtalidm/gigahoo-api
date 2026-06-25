using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
using Gigahoo.Api.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

/// <summary>
/// Service endpoints for the voice agent to fetch account config and save call data.
/// </summary>
[ApiController]
[Route("api/voice-agent")]
public class VoiceAgentController(
    GigahooDbContext db,
    ISmsProvider smsProvider,
    IEmailService email,
    ILogger<VoiceAgentController> logger) : ControllerBase
{
    /// <summary>
    /// Get account configuration for a specific account (used by voice agent before a call)
    /// </summary>
    [HttpGet("account/{accountId:guid}")]
    public async Task<ActionResult<VoiceAgentAccountResponse>> GetAccountConfig(Guid accountId)
    {
        var account = await db.Accounts
            .Include(a => a.Category)
            .Include(a => a.Region)
            .Include(a => a.Plan)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account is null) return NotFound(new { error = "Account not found" });

        var minutesRemaining = (account.Plan?.IncludedMinutes ?? 0) - account.MinutesUsed;
        var canAnswer = minutesRemaining > 0;

        string? regionName = null;
        if (account.RegionId.HasValue)
        {
            regionName = account.Region?.Name ?? account.RegionCustom;
        }
        else
        {
            regionName = account.RegionCustom;
        }

        return Ok(new VoiceAgentAccountResponse(
            account.Id,
            account.BusinessName,
            account.Category?.Name ?? "Other",
            account.Category?.ServiceDescription ?? "service need",
            account.BusinessPhone,
            account.Email,
            account.ServiceArea,
            account.BusinessHours,
            account.WebsiteUrl,
            account.AddressLine1,
            account.City,
            regionName,
            account.PostalCode,
            await db.Countries.FindAsync(account.CountryCodeId) is { } country ? country.Name : "",
            new VoiceAgentFeatureSettings(
                account.AnswerQuestions,
                account.ServicesInfo,
                account.ServeArea,
                account.DistanceKm,
                account.QuoteInspection,
                account.PricePerKm
            ),
            minutesRemaining,
            canAnswer
        ));
    }

    /// <summary>
    /// Create a conversation record after a call ends (used by voice agent)
    /// </summary>
    [HttpPost("conversations/{accountId:guid}")]
    public async Task<IActionResult> CreateConversation(Guid accountId, [FromBody] CreateConversationRequest request)
    {
        var account = await db.Accounts
            .Include(a => a.Plan)
            .FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null) return NotFound(new { error = "Account not found" });

        var conversation = new Conversation
        {
            AccountId = accountId,
            CallerName = request.CallerName,
            CallerPhone = request.CallerPhone,
            DateTimeUtc = DateTime.UtcNow,
            DurationSeconds = request.DurationSeconds,
            LanguageId = request.LanguageId ?? 1, // Default to English
            Summary = request.Summary,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
        };

        db.Conversations.Add(conversation);

        // Meter minutes for this call, rounding UP to the next whole minute.
        account.MinutesUsed += (int)Math.Ceiling(request.DurationSeconds / 60.0);

        var remaining = (account.Plan?.IncludedMinutes ?? 0) - account.MinutesUsed;

        // Notify the owner once per billing period when they first cross their limit.
        if (remaining <= 0 && account.LimitNotifiedAt is null)
        {
            var ownerPhone = account.PhoneNumber ?? account.ForwardingPhone;
            if (!string.IsNullOrWhiteSpace(ownerPhone))
            {
                try
                {
                    await smsProvider.SendAsync(
                        ownerPhone,
                        "Your Gigahoo AI receptionist has used all included minutes for this period. Upgrade to keep answering calls.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send minutes-exhausted SMS for account {Account}", account.Id);
                }
            }

            if (!string.IsNullOrWhiteSpace(account.Email))
            {
                try
                {
                    await email.SendMinutesExhaustedAsync(account.Email, account.BusinessName ?? "there");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send minutes-exhausted email for account {Account}", account.Id);
                }
            }

            account.LimitNotifiedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new { conversationId = conversation.Id });
    }
}

public record VoiceAgentAccountResponse(
    Guid Id,
    string BusinessName,
    string Category,
    string ServiceDescription,
    string BusinessPhone,
    string Email,
    string? ServiceArea,
    string? BusinessHours,
    string? WebsiteUrl,
    string? AddressLine1,
    string? City,
    string? Region,
    string? PostalCode,
    string Country,
    VoiceAgentFeatureSettings Features,
    int MinutesRemaining,
    bool CanAnswer
);

public record VoiceAgentFeatureSettings(
    bool AnswerQuestions,
    string? ServicesInfo,
    bool ServeArea,
    int DistanceKm,
    bool QuoteInspection,
    decimal PricePerKm
);

public record CreateConversationRequest(
    string? CallerName,
    string CallerPhone,
    int DurationSeconds,
    byte? LanguageId,
    string? Summary,
    string Status
);
