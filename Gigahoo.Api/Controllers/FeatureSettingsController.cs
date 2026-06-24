using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/features")]
[Authorize]
[EnableRateLimiting("api")]
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

        return Ok(new FeatureSettingsResponse(
            account.AnswerQuestions,
            account.ServicesInfo,
            account.FeatureServiceAreas,
            account.FeatureBusinessHours,
            account.EmergencyAvailability,
            account.PricingPolicy,
            account.WarrantyPolicy,
            account.FrequentlyAskedQuestions,
            account.AdditionalBusinessInfo,
            account.ServeArea,
            account.DistanceKm,
            account.QuoteInspection,
            account.PricePerKm
        ));
    }

    [HttpPut]
    public async Task<ActionResult<FeatureSettingsResponse>> Update([FromBody] UpdateFeatureSettingsRequest request)
    {
        var accountId = GetAccountId();

        var account = await db.Accounts.Include(a => a.Plan).FirstAsync(a => a.Id == accountId);
        if (account.Plan.Id != 3)
            return StatusCode(403, new { error = "Optional features require the Business plan" });

        account.AnswerQuestions = request.AnswerQuestions;
        account.ServicesInfo = request.ServicesInfo;
        account.FeatureServiceAreas = request.ServiceAreas;
        account.FeatureBusinessHours = request.BusinessHours;
        account.EmergencyAvailability = request.EmergencyAvailability;
        account.PricingPolicy = request.PricingPolicy;
        account.WarrantyPolicy = request.WarrantyPolicy;
        account.FrequentlyAskedQuestions = request.FrequentlyAskedQuestions;
        account.AdditionalBusinessInfo = request.AdditionalBusinessInfo;
        account.ServeArea = request.ServeArea;
        account.DistanceKm = Math.Clamp(request.DistanceKm, 1, 1000);
        account.QuoteInspection = request.QuoteInspection;
        account.PricePerKm = request.PricePerKm;
        account.FeatureUpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new FeatureSettingsResponse(
            account.AnswerQuestions,
            account.ServicesInfo,
            account.FeatureServiceAreas,
            account.FeatureBusinessHours,
            account.EmergencyAvailability,
            account.PricingPolicy,
            account.WarrantyPolicy,
            account.FrequentlyAskedQuestions,
            account.AdditionalBusinessInfo,
            account.ServeArea,
            account.DistanceKm,
            account.QuoteInspection,
            account.PricePerKm
        ));
    }
}
