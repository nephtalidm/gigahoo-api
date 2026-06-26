using Gigahoo.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/countries")]
[EnableRateLimiting("api")]
public class CountriesController(GigahooDbContext db) : ControllerBase
{
    // Supported (served) countries as ISO-2 codes, e.g. ["US","CA"]. Non-sensitive
    // and consumed by the public signup/auth pickers, so it's intentionally anonymous.
    [HttpGet("supported")]
    public async Task<ActionResult<List<string>>> GetSupported()
    {
        return Ok(await db.Countries
            .Where(c => c.IsSupported)
            .OrderBy(c => c.Code)
            .Select(c => c.Code.Trim())
            .ToListAsync());
    }
}
