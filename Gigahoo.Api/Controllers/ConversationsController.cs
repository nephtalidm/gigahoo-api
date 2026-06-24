using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("api")]
public class ConversationsController(GigahooDbContext db) : ControllerBase
{
    private Guid GetAccountId() => Guid.Parse(User.FindFirst("account_id")!.Value);

    [HttpGet]
    public async Task<ActionResult<ConversationsPageResponse>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var accountId = GetAccountId();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Conversations
            .Include(c => c.Language)
            .Where(c => c.AccountId == accountId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);

        var totalCount = await query.CountAsync();

        var conversations = await query
            .OrderByDescending(c => c.DateTimeUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationResponse(
                c.Id,
                c.CallerName,
                c.CallerPhone,
                c.DateTimeUtc,
                c.DurationSeconds,
                c.Language != null ? c.Language.Name : "English",
                c.Summary,
                c.Status
            ))
            .ToListAsync();

        return Ok(new ConversationsPageResponse(conversations, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConversationResponse>> GetConversation(Guid id)
    {
        var accountId = GetAccountId();

        var conversation = await db.Conversations
            .Include(c => c.Language)
            .FirstOrDefaultAsync(c => c.Id == id && c.AccountId == accountId);

        if (conversation is null) return NotFound();

        return Ok(new ConversationResponse(
            conversation.Id,
            conversation.CallerName,
            conversation.CallerPhone,
            conversation.DateTimeUtc,
            conversation.DurationSeconds,
            conversation.Language?.Name ?? "English",
            conversation.Summary,
            conversation.Status
        ));
    }
}
