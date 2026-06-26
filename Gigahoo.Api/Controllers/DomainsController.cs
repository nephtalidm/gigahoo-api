using Gigahoo.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/domains")]
[EnableRateLimiting("api")]
public class DomainsController(GigahooDbContext db) : ControllerBase
{
    // Regional domains as { host, countryCode } pairs. A null countryCode means
    // "geo-detect" (no market pinned). Non-sensitive and consumed by the public
    // UI middleware (host -> forced country), so it's intentionally anonymous.
    [HttpGet]
    public async Task<ActionResult> GetDomains()
    {
        return Ok(await db.Domains
            .OrderBy(d => d.Host)
            .Select(d => new { host = d.Host, countryCode = d.CountryCode })
            .ToListAsync());
    }
}
