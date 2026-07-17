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
            .FirstOrDefaultAsync(a => a.AccountId == accountId);

        if (account is null) return NotFound(new { error = "Account not found" });

        var minutesRemaining = (account.Plan?.IncludedMinutes ?? 0) - account.MinutesUsed;
        var canAnswer = minutesRemaining > 0;

        // Greeting precedence: account's custom greeting, then the site-wide
        // DefaultGreeting setting, then a hard-coded fallback.
        var greetingMessage = account.GreetingMessage;
        if (string.IsNullOrWhiteSpace(greetingMessage))
        {
            greetingMessage = await db.Settings
                .Where(s => s.SettingKey == "DefaultGreeting")
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync();
        }
        if (string.IsNullOrWhiteSpace(greetingMessage))
        {
            greetingMessage = "Hi, thanks for calling! How can I help you today?";
        }

        // Substitute the business-name placeholder so the voice agent receives the
        // greeting with the real business name baked in.
        greetingMessage = greetingMessage.Replace("[Name of business]", account.BusinessName);

        string? regionName = null;
        if (account.RegionId.HasValue)
        {
            regionName = account.Region?.Name;
        }

        // The omni realtime session only accepts a qwen voice. If the account picked a CosyVoice
        // voice (for the TTS pipeline), it must NOT be sent to the omni or the session errors —
        // fall back to the omni default (null) here so live calls never break on a TTS voice.
        var omniVoice = account.AgentVoice;
        if (omniVoice is not null && !await db.Voices.AnyAsync(v =>
                v.IsActive && v.ApiName == omniVoice && v.Provider.Code == "qwen" && v.Provider.ProviderTypeId == 1))
            omniVoice = null;

        return Ok(new VoiceAgentAccountResponse(
            account.AccountId,
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
            minutesRemaining,
            canAnswer,
            greetingMessage,
            omniVoice,
            account.MaximumCallMinutes,
            account.CollectName,
            account.CollectPhone,
            account.CollectAddress,
            account.CollectEmergency,
            account.AgentStyle,
            account.AgentInstruct,
            account.AccountLanguage
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
            .Include(a => a.Region)
            .FirstOrDefaultAsync(a => a.AccountId == accountId);
        if (account is null) return NotFound(new { error = "Account not found" });

        var conversation = new Conversation
        {
            AccountId = accountId,
            CallerName = request.CallerName,
            CallerPhoneNumber = request.CallerPhoneNumber,
            DateTimeUtc = DateTime.UtcNow,
            DurationSeconds = request.DurationSeconds,
            LanguageId = request.LanguageId ?? 1, // Default to English
            Summary = request.Summary,
            Address = request.Address,
            IsEmergency = request.IsEmergency,
            ConversationStatusId = (byte)(Enum.TryParse<Entities.ConversationStatusId>(request.Status, ignoreCase: true, out var cs)
                ? cs : Entities.ConversationStatusId.Missed),
            ConversationTypeId = (byte)Entities.ConversationTypeId.PhoneCall,
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
                    logger.LogError(ex, "Failed to send minutes-exhausted SMS for account {Account}", account.AccountId);
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
                    logger.LogError(ex, "Failed to send minutes-exhausted email for account {Account}", account.AccountId);
                }
            }

            account.LimitNotifiedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // Best-effort post-call summary to the owner per their notification settings.
        // Each channel is isolated in try/catch so a delivery failure never fails the call save.
        if (account.EmailCallNotifications && !string.IsNullOrWhiteSpace(account.Email))
        {
            try
            {
                await email.SendCallSummaryAsync(
                    account.Email,
                    account.BusinessName ?? "there",
                    request.CallerName,
                    request.CallerPhoneNumber,
                    request.Address,
                    request.Language,
                    request.DurationSeconds,
                    Gigahoo.Api.Services.TimeZoneResolver.FormatLocal(conversation.DateTimeUtc, account.Region?.Name),
                    request.Summary);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send call-summary email for account {Account}", account.AccountId);
            }
        }

        if (account.SmsCallNotifications)
        {
            var ownerPhone = account.PhoneNumber ?? account.ForwardingPhone;
            if (!string.IsNullOrWhiteSpace(ownerPhone))
            {
                try
                {
                    var mmss = $"{request.DurationSeconds / 60}:{request.DurationSeconds % 60:D2}";
                    // The address is the RECORD's address — Google's canonical format (or the
                    // caller's insisted version), same as the dashboard and the email.
                    var addressLine = string.IsNullOrWhiteSpace(request.Address) ? "" : $"\n{request.Address}";
                    var text = $"New Gigahoo call — {request.CallerName ?? "Unknown"} ({request.CallerPhoneNumber}), {mmss} min.{addressLine}\n{request.Summary}";
                    await smsProvider.SendAsync(ownerPhone, text);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send call-summary SMS for account {Account}", account.AccountId);
                }
            }
        }

        return Ok(new { conversationId = conversation.ConversationId });
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
    int MinutesRemaining,
    bool CanAnswer,
    string GreetingMessage,
    string? AgentVoice,
    int? MaximumCallMinutes,
    bool CollectName,
    bool CollectPhone,
    bool CollectAddress,
    bool CollectEmergency,
    string? AgentStyle,
    string? AgentInstruct,
    string? AccountLanguage
);

public record CreateConversationRequest(
    string? CallerName,
    string CallerPhoneNumber,
    int DurationSeconds,
    byte? LanguageId,
    string? Summary,
    string Status,
    string? Address = null,
    string? Language = null,
    bool IsEmergency = false
);
