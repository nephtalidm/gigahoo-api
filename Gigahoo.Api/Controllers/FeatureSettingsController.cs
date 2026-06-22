using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/features")]
[Authorize]
public class FeatureSettingsController(GigahooDbContext db) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpGet]
    public async Task<ActionResult<FeatureSettingsResponse>> Get()
    {
        var accountId = GetAccountId();

        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.Id == accountId);
        if (account.Plan.Id != 3) // Business only
            return StatusCode(403, new { error = "Optional features require the Business plan" });

        var settings = await db.FeatureSettings.FirstOrDefaultAsync(f => f.AccountId == accountId);
        if (settings is null)
        {
            return Ok(new FeatureSettingsResponse(false, null, false, 50, false, 0));
        }

        return Ok(new FeatureSettingsResponse(
            settings.AnswerQuestions,
            settings.ServicesInfo,
            settings.ServeArea,
            settings.DistanceKm,
            settings.QuoteInspection,
            settings.PricePerKm
        ));
    }

    [HttpPut]
    public async Task<ActionResult<FeatureSettingsResponse>> Update([FromBody] UpdateFeatureSettingsRequest request)
    {
        var accountId = GetAccountId();

        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.Id == accountId);
        if (account.Plan.Id != 3)
            return StatusCode(403, new { error = "Optional features require the Business plan" });

        var settings = await db.FeatureSettings.FirstOrDefaultAsync(f => f.AccountId == accountId);

        if (settings is null)
        {
            settings = new FeatureSettings
            {
                AccountId = accountId,
                AnswerQuestions = request.AnswerQuestions,
                ServicesInfo = request.ServicesInfo,
                ServeArea = request.ServeArea,
                DistanceKm = Math.Clamp(request.DistanceKm, 1, 1000),
                QuoteInspection = request.QuoteInspection,
                PricePerKm = request.PricePerKm,
                UpdatedAt = DateTime.UtcNow,
            };
            db.FeatureSettings.Add(settings);
        }
        else
        {
            settings.AnswerQuestions = request.AnswerQuestions;
            settings.ServicesInfo = request.ServicesInfo;
            settings.ServeArea = request.ServeArea;
            settings.DistanceKm = Math.Clamp(request.DistanceKm, 1, 1000);
            settings.QuoteInspection = request.QuoteInspection;
            settings.PricePerKm = request.PricePerKm;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new FeatureSettingsResponse(
            settings.AnswerQuestions,
            settings.ServicesInfo,
            settings.ServeArea,
            settings.DistanceKm,
            settings.QuoteInspection,
            settings.PricePerKm
        ));
    }
}
