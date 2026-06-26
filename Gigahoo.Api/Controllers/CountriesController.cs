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

    // The visitor's pricing currency, taken from Country.Currency in the DB — for
    // ANY country (including coming-soon markets like Mexico -> MXN), not just
    // signup-supported ones. Unknown countries fall back to the default market
    // (US)'s currency. No currency is hardcoded — it always comes from data.
    [HttpGet("currency")]
    public async Task<ActionResult> GetCurrency([FromQuery] string? code)
    {
        var match = await db.Countries.FirstOrDefaultAsync(c => c.Code == (code ?? ""));
        var currency = match?.Currency
            ?? (await db.Countries.FirstOrDefaultAsync(c => c.Code == "US"))?.Currency;
        return Ok(new { currency });
    }
}
