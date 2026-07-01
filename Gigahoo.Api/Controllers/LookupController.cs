using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class LookupController(GigahooDbContext db) : ControllerBase
{
    // When supportedOnly=true, restrict to served markets (Country.IsSupported) —
    // used by the address country pickers so the filtering lives in the backend.
    [HttpGet("countries")]
    public async Task<ActionResult<List<CountryResponse>>> GetCountries([FromQuery] bool supportedOnly = false)
    {
        var query = db.Countries.AsQueryable();
        if (supportedOnly)
            query = query.Where(c => c.IsSupported);

        var countries = await query
            .OrderBy(c => c.Name)
            .Select(c => new CountryResponse(c.CountryId, c.Name, c.Code, c.DialCode, c.Flag))
            .ToListAsync();

        return Ok(countries);
    }

    [HttpGet("countries/{countryId}/regions")]
    public async Task<ActionResult<List<RegionResponse>>> GetRegions(short countryId)
    {
        var regions = await db.Regions
            .Where(r => r.CountryId == countryId)
            .OrderBy(r => r.Name)
            .Select(r => new RegionResponse(r.RegionId, r.Name, r.Code))
            .ToListAsync();

        return Ok(regions);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<BusinessCategoryResponse>>> GetCategories()
    {
        var categories = await db.BusinessCategories
            .OrderBy(c => c.Name)
            .Select(c => new BusinessCategoryResponse(c.BusinessCategoryId, c.Name))
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("languages")]
    public async Task<ActionResult<List<LanguageResponse>>> GetLanguages()
    {
        var languages = await db.Languages
            .OrderBy(l => l.Name)
            .Select(l => new LanguageResponse(l.LanguageId, l.Name))
            .ToListAsync();

        return Ok(languages);
    }
}
