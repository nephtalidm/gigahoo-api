using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CallsController(GigahooDbContext db) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpGet]
    public async Task<ActionResult<CallsPageResponse>> GetCalls(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var accountId = GetAccountId();

        var query = db.Calls
            .Include(c => c.Language)
            .Include(c => c.CollectedInfo)
            .Where(c => c.AccountId == accountId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);

        var totalCount = await query.CountAsync();

        var calls = await query
            .OrderByDescending(c => c.DateTimeUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CallResponse(
                c.Id,
                c.CallerName,
                c.CallerPhone,
                c.DateTimeUtc,
                c.DurationSeconds,
                c.Language != null ? c.Language.Name : "English",
                c.Summary,
                c.Status,
                c.CollectedInfo.Select(ci => new CollectedInfoDto(ci.Label, ci.Value)).ToList()
            ))
            .ToListAsync();

        return Ok(new CallsPageResponse(calls, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CallResponse>> GetCall(Guid id)
    {
        var accountId = GetAccountId();

        var call = await db.Calls
            .Include(c => c.Language)
            .Include(c => c.CollectedInfo)
            .FirstOrDefaultAsync(c => c.Id == id && c.AccountId == accountId);

        if (call is null) return NotFound();

        return Ok(new CallResponse(
            call.Id,
            call.CallerName,
            call.CallerPhone,
            call.DateTimeUtc,
            call.DurationSeconds,
            call.Language?.Name ?? "English",
            call.Summary,
            call.Status,
            call.CollectedInfo.Select(ci => new CollectedInfoDto(ci.Label, ci.Value)).ToList()
        ));
    }
}
