using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

/// <summary>
/// Service endpoints for the voice agent to fetch account config and save call data.
/// </summary>
[ApiController]
[Route("api/voice-agent")]
public class VoiceAgentController(GigahooDbContext db) : ControllerBase
{
    /// <summary>
    /// Get account configuration for a specific account (used by voice agent before a call)
    /// </summary>
    [HttpGet("account/{accountId:guid}")]
    public async Task<ActionResult<VoiceAgentAccountResponse>> GetAccountConfig(Guid accountId)
    {
        var account = await db.Accounts
            .Include(a => a.Category)
            .Include(a => a.Country)
            .Include(a => a.Region)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account is null) return NotFound(new { error = "Account not found" });

        var featureSettings = await db.FeatureSettings
            .FirstOrDefaultAsync(f => f.AccountId == accountId);

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
            account.Country?.Name ?? "",
            new VoiceAgentFeatureSettings(
                featureSettings?.AnswerQuestions ?? false,
                featureSettings?.ServicesInfo,
                featureSettings?.ServeArea ?? false,
                featureSettings?.DistanceKm ?? 0,
                featureSettings?.QuoteInspection ?? false,
                featureSettings?.PricePerKm ?? 0
            )
        ));
    }

    /// <summary>
    /// Create a call record after a call ends (used by voice agent)
    /// </summary>
    [HttpPost("calls/{accountId:guid}")]
    public async Task<IActionResult> CreateCall(Guid accountId, [FromBody] CreateCallRequest request)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null) return NotFound(new { error = "Account not found" });

        var call = new Call
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

        db.Calls.Add(call);
        await db.SaveChangesAsync();

        // Add collected info
        if (request.CollectedInfo?.Any() == true)
        {
            foreach (var info in request.CollectedInfo)
            {
                db.CallCollectedInfo.Add(new CallCollectedInfo
                {
                    CallId = call.Id,
                    Label = info.Label,
                    Value = info.Value,
                });
            }
            await db.SaveChangesAsync();
        }

        return Ok(new { callId = call.Id });
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
    VoiceAgentFeatureSettings Features
);

public record VoiceAgentFeatureSettings(
    bool AnswerQuestions,
    string? ServicesInfo,
    bool ServeArea,
    int DistanceKm,
    bool QuoteInspection,
    decimal PricePerKm
);

public record CreateCallRequest(
    string? CallerName,
    string CallerPhone,
    int DurationSeconds,
    byte? LanguageId,
    string? Summary,
    string Status,
    List<CollectedInfoDto>? CollectedInfo
);
