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
        var isValidVoice = !string.IsNullOrWhiteSpace(voice) && await db.Voices.AnyAsync(v =>
            v.IsActive && v.ApiName == voice &&
            v.Provider.Code == "qwen" && v.Provider.ProviderTypeId == 1);
        if (!isValidVoice)
            return BadRequest(new { error = "Invalid voice" });

        try
        {
            var wav = await voiceSamples.SynthesizeAsync(text, voice!, HttpContext.RequestAborted);
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

public record VoiceSampleRequest(string? Text, string? Voice);
