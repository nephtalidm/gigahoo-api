using Gigahoo.Api.Data;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[EnableRateLimiting("api")]
public class VoicesController(GigahooDbContext db) : ControllerBase
{
    /// <summary>
    /// The active AI-agent voices (Fish Audio — the engine that speaks live calls) for the
    /// dashboard voice picker: tagged with gender and language, ordered by language then
    /// DisplayOrder. Not sensitive, so anonymous-readable.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<VoiceResponse>>> Get()
    {
        var rows = await db.AgentVoices
            .Where(v => v.IsActive && v.Provider.Code == "fish" && v.Provider.ProviderTypeId == 1)
            .OrderBy(v => v.LanguageId).ThenBy(v => v.DisplayOrder)
            .Select(v => new
            {
                v.ReferenceId,
                v.Label,
                v.IsDefault,
                v.Gender,
                Language = v.Language != null ? v.Language.Name : null,
                LanguageCode = v.Language != null ? v.Language.Code : null,
            })
            .ToListAsync();

        // Emotion is handled adaptively by the agent (voice tags in the reply text), so no
        // per-voice instruct options are attached.
        var voices = rows
            .Select(v => new VoiceResponse(v.ReferenceId, v.Label, v.IsDefault, v.Gender, v.Language, v.LanguageCode, Array.Empty<InstructOption>()))
            .ToList();

        return Ok(voices);
    }

    /// <summary>
    /// Voice-lab test data: the instruct-capable CosyVoice + Qwen-TTS voices plus their working
    /// emotion/preset sets, so the dedicated testing page can preview what actually renders. These
    /// are sample-only voices — they do NOT drive live calls.
    /// </summary>
    [HttpGet("lab")]
    public async Task<ActionResult<LabResponse>> Lab()
    {
        var cosy = await db.AgentVoices
            .Where(v => v.IsActive && v.Provider.Code == "cosyvoice" && v.Provider.ProviderTypeId == 1)
            .OrderBy(v => v.DisplayOrder)
            .Select(v => new LabVoice(v.ReferenceId, v.Label))
            .ToListAsync();
        var qwen = await db.AgentVoices
            .Where(v => v.IsActive && v.Provider.Code == "qwen-tts" && v.Provider.ProviderTypeId == 1)
            .OrderBy(v => v.DisplayOrder)
            .Select(v => new LabVoice(v.ReferenceId, v.Label))
            .ToListAsync();

        // CosyVoice emotions, working ones first (sad/fearful render best; high-arousal barely at all).
        string[] emotions = ["neutral", "sad", "fearful", "surprised", "happy", "angry", "disgusted"];
        return Ok(new LabResponse(cosy, emotions, qwen, QwenInstructs.Options()));
    }
}

public record VoiceResponse(string ApiName, string Label, bool IsDefault, string? Gender, string? Language, string? LanguageCode, IReadOnlyList<InstructOption> Options);
public record LabVoice(string ApiName, string Label);
public record LabResponse(
    IReadOnlyList<LabVoice> Cosyvoice,
    IReadOnlyList<string> CosyvoiceEmotions,
    IReadOnlyList<LabVoice> QwenTts,
    IReadOnlyList<InstructOption> QwenPresets);
