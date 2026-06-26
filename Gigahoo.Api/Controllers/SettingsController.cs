using Gigahoo.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/settings")]
[EnableRateLimiting("api")]
public class SettingsController(GigahooDbContext db) : ControllerBase
{
    // Public website settings consumed by the dashboard UI. Non-sensitive
    // key/value pairs (e.g. the default AI voice agent greeting used to
    // pre-fill the greeting input), so it's intentionally anonymous.
    [HttpGet]
    public async Task<ActionResult> Get()
    {
        var defaultGreeting = await db.Settings
            .Where(s => s.SettingKey == "DefaultGreeting")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync();
        return Ok(new { defaultGreeting });
    }
}
