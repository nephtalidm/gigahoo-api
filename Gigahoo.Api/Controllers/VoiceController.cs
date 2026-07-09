using Gigahoo.Api.Data;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/voice")]
[Authorize]
[EnableRateLimiting("api")]
public class VoiceController(
    GigahooDbContext db,
    IVoiceSampleService voiceSamples,
    ICosyVoiceService cosyVoice,
    ILogger<VoiceController> logger) : ControllerBase
{
    /// <summary>
    /// Synthesize the given greeting text in the given voice and return WAV audio,
    /// so the dashboard can play a live sample of the user's actual greeting.
    /// </summary>
    [HttpPost("sample")]
    public async Task<IActionResult> Sample([FromBody] VoiceSampleRequest request)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "Text is required" });
        if (text.Length > 500)
            return BadRequest(new { error = "Text must be 500 characters or fewer" });

        // Validate the voice against the ACTIVE voices the LLM provider (Qwen) offers — the same
        // data-driven check as saving the agent voice, so the preview allow-list can't drift.
        var voice = request.Voice?.Trim();
        var voiceRow = string.IsNullOrWhiteSpace(voice) ? null : await db.Voices
            .Include(v => v.Provider)
            .FirstOrDefaultAsync(v => v.IsActive && v.ApiName == voice && v.Provider.ProviderTypeId == 1);
        if (voiceRow is null)
            return BadRequest(new { error = "Invalid voice" });

        try
        {
            byte[] wav;
            if (voiceRow.Provider.Code == "cosyvoice")
            {
                // Emotion + optional scenario/role context → the required Chinese instruct template.
                var instruction = InstructCatalog.Build(voice!, request.Style, request.Instruct);
                wav = await cosyVoice.SynthesizeAsync(text, voice!, instruction, HttpContext.RequestAborted);
            }
            else
            {
                wav = await voiceSamples.SynthesizeAsync(text, voice!, request.Style, HttpContext.RequestAborted);
            }
            return File(wav, "audio/wav");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Voice sample synthesis failed");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice sample synthesis failed");
            return StatusCode(500, new { error = "Voice synthesis failed" });
        }
    }
}

public record VoiceSampleRequest(string? Text, string? Voice, string? Style, string? Instruct);
