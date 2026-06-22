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
    [HttpGet("countries")]
    public async Task<ActionResult<List<CountryResponse>>> GetCountries()
    {
        var countries = await db.Countries
            .OrderBy(c => c.Name)
            .Select(c => new CountryResponse(c.Id, c.Name, c.Code, c.DialCode, c.Flag))
            .ToListAsync();

        return Ok(countries);
    }

    [HttpGet("countries/{countryId}/regions")]
    public async Task<ActionResult<List<RegionResponse>>> GetRegions(short countryId)
    {
        var regions = await db.Regions
            .Where(r => r.CountryId == countryId)
            .OrderBy(r => r.Name)
            .Select(r => new RegionResponse(r.Id, r.Name, r.Code))
            .ToListAsync();

        return Ok(regions);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<BusinessCategoryResponse>>> GetCategories()
    {
        var categories = await db.BusinessCategories
            .OrderBy(c => c.Name)
            .Select(c => new BusinessCategoryResponse(c.Id, c.Name))
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("languages")]
    public async Task<ActionResult<List<LanguageResponse>>> GetLanguages()
    {
        var languages = await db.Languages
            .OrderBy(l => l.Name)
            .Select(l => new LanguageResponse(l.Id, l.Name))
            .ToListAsync();

        return Ok(languages);
    }
}
