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
    /// The active AI-agent voices for the LLM provider (Qwen), for the dashboard
    /// voice picker. Ordered by DisplayOrder. Not sensitive, so anonymous-readable.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<VoiceResponse>>> Get()
    {
        var rows = await db.Voices
            .Where(v => v.IsActive && v.Provider.Code == "cosyvoice" && v.Provider.ProviderTypeId == 1)
            .OrderBy(v => v.DisplayOrder)
            .Select(v => new { v.ApiName, v.Label, v.IsDefault })
            .ToListAsync();

        // Attach each voice's instruct "context" options (scenarios/roles/identities) from the catalog.
        var voices = rows
            .Select(v => new VoiceResponse(v.ApiName, v.Label, v.IsDefault, InstructCatalog.OptionsFor(v.ApiName)))
            .ToList();

        return Ok(voices);
    }
}

public record VoiceResponse(string ApiName, string Label, bool IsDefault, IReadOnlyList<InstructOption> Options);
