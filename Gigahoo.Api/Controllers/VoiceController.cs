using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/voice")]
[Authorize]
[EnableRateLimiting("api")]
public class VoiceController(IVoiceSampleService voiceSamples, ILogger<VoiceController> logger) : ControllerBase
{
    // The valid qwen3.5-omni-plus-realtime voices the UI offers.
    private static readonly HashSet<string> ValidVoices = new(StringComparer.Ordinal)
    {
        "Tina", "Ethan", "Jennifer", "Ryan", "Aiden", "Cindy", "Raymond"
    };

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
        if (string.IsNullOrWhiteSpace(request.Voice) || !ValidVoices.Contains(request.Voice))
            return BadRequest(new { error = "Invalid voice" });

        try
        {
            var wav = await voiceSamples.SynthesizeAsync(text, request.Voice, HttpContext.RequestAborted);
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
