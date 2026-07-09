using Gigahoo.Api.Data;
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
    /// The active AI-agent voices for the LLM provider (Qwen), for the dashboard
    /// voice picker. Ordered by DisplayOrder. Not sensitive, so anonymous-readable.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<VoiceResponse>>> Get()
    {
        var voices = await db.Voices
            .Where(v => v.IsActive && v.Provider.Code == "cosyvoice" && v.Provider.ProviderTypeId == 1)
            .OrderBy(v => v.DisplayOrder)
            .Select(v => new VoiceResponse(v.ApiName, v.Label, v.IsDefault))
            .ToListAsync();

        return Ok(voices);
    }
}

public record VoiceResponse(string ApiName, string Label, bool IsDefault);
