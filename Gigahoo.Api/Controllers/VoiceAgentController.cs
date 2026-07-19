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
    IConfiguration config,
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
            greetingMessage = config["Defaults:DefaultGreeting"];
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
        var voiceRow = account.AgentVoiceId is null ? null : await db.AgentVoices
            .Include(v => v.Provider)
            .FirstOrDefaultAsync(v => v.AgentVoiceId == account.AgentVoiceId);
        // Fish TTS voices for the split-voice pipeline: the account's pick is the PRIMARY
        // (greeting + fallback), and each language maps to its own voice (the account's pick
        // wins for its language; otherwise the language's IsDefault voice) so the agent can
        // switch voices when the caller's language changes.
        var fishVoices = await db.AgentVoices
            .Include(v => v.Language)
            .Where(v => v.IsActive && v.Provider!.Code == "fish" && v.Provider.ProviderTypeId == 1)
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync();
        var primaryFish = (voiceRow is not null && voiceRow.Provider!.Code == "fish" ? voiceRow : null)
            ?? fishVoices.FirstOrDefault(v => v.IsDefault && v.Language?.Code == "en")
            ?? fishVoices.FirstOrDefault(v => v.IsDefault)
            ?? fishVoices.FirstOrDefault();
        var ttsVoicesByLanguage = fishVoices
            .Where(v => v.Language?.Code is not null)
            .GroupBy(v => v.Language!.Code!)
            .ToDictionary(
                g => g.Key,
                g => (primaryFish is not null && g.Any(v => v.AgentVoiceId == primaryFish.AgentVoiceId)
                        ? g.First(v => v.AgentVoiceId == primaryFish.AgentVoiceId)
                        : g.FirstOrDefault(v => v.IsDefault) ?? g.First()).ReferenceId);

        var omniVoice = voiceRow?.ReferenceId;
        if (omniVoice is not null && !(voiceRow!.IsActive && voiceRow.Provider!.Code == "qwen" && voiceRow.Provider.ProviderTypeId == 1))
            omniVoice = null;

        return Ok(new VoiceAgentAccountResponse(
            account.AccountId,
            account.BusinessName,
            account.Category?.Name ?? "Other",
            account.Category?.ServiceDescription ?? "service need",
            account.BusinessPhoneNumber,
            account.Email,
            account.BusinessHours,
            account.WebsiteUrl,
            account.AddressLine1,
            account.City,
            regionName,
            account.PostalCode,
            await db.Countries.FindAsync(account.CountryCodeId) is { } country ? country.Name : "",
            primaryFish?.ReferenceId,
            ttsVoicesByLanguage,
            minutesRemaining,
            canAnswer,
            greetingMessage,
            account.BusinessKnowledge,
            omniVoice,
            account.MaximumCallMinutes,
            account.ShouldCollectName,
            account.ShouldCollectPhone,
            account.ShouldCollectAddress,
            account.ShouldCollectEmergency,
            account.AccountLanguageId is null ? null : await db.Languages
                .Where(l => l.LanguageId == account.AccountLanguageId)
                .Select(l => l.Code)
                .FirstOrDefaultAsync()
        ));
    }

    /// <summary>
    /// PER-NUMBER ROUTING: resolve the account that OWNS the dialed number (E.164) and
    /// return its config. The PhoneNumber pool's AssignedAccountId is the single source of
    /// truth — no env-pinned default account.
    /// </summary>
    [HttpGet("account/by-number/{number}")]
    public async Task<ActionResult<VoiceAgentAccountResponse>> GetAccountConfigByNumber(string number)
    {
        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length < 7) return NotFound(new { error = "Invalid number" });
        var accountId = await db.PhoneNumbers
            .Where(p => p.AssignedAccountId != null && p.Number.Replace("+", "") == digits)
            .Select(p => p.AssignedAccountId)
            .FirstOrDefaultAsync();
        if (accountId is null) return NotFound(new { error = "No account owns this number" });
        return await GetAccountConfig(accountId.Value);
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
            DurationSeconds = request.DurationSeconds,
            LanguageId = request.LanguageId ?? 1, // Default to English
            Summary = request.Summary,
            Transcript = request.Transcript,
            Address = request.Address,
            IsEmergency = request.IsEmergency,
            ConversationStatusId = (byte)(Enum.TryParse<Entities.ConversationStatusId>(request.Status, ignoreCase: true, out var cs)
                ? cs : Entities.ConversationStatusId.Missed),
            ConversationTypeId = (byte)Entities.ConversationTypeId.PhoneCall,
            CreatedDate = DateTime.UtcNow,
        };

        db.Conversations.Add(conversation);

        // Meter minutes for this call, rounding UP to the next whole minute.
        account.MinutesUsed += (int)Math.Ceiling(request.DurationSeconds / 60.0);

        var remaining = (account.Plan?.IncludedMinutes ?? 0) - account.MinutesUsed;

        // Notify the owner once per billing period when they first cross their limit.
        if (remaining <= 0 && account.LimitNotifiedAt is null)
        {
            var ownerPhone = account.BusinessPhoneNumber;
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
        if (account.ShouldSendCallSummaryEmail && !string.IsNullOrWhiteSpace(account.Email))
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
                    Gigahoo.Api.Services.TimeZoneResolver.FormatLocal(conversation.CreatedDate, account.Region?.Name),
                    request.Summary);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send call-summary email for account {Account}", account.AccountId);
            }
        }

        if (account.ShouldSendCallSummarySms)
        {
            var ownerPhone = account.BusinessPhoneNumber;
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
    string? BusinessHours,
    string? WebsiteUrl,
    string? AddressLine1,
    string? City,
    string? Region,
    string? PostalCode,
    string Country,
    string? TtsVoice,
    Dictionary<string, string> TtsVoicesByLanguage,
    int MinutesRemaining,
    bool CanAnswer,
    string GreetingMessage,
    string? BusinessKnowledge,
    string? AgentVoice,
    int? MaximumCallMinutes,
    bool CollectName,
    bool CollectPhone,
    bool CollectAddress,
    bool CollectEmergency,
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
    bool IsEmergency = false,
    string? Transcript = null
);
