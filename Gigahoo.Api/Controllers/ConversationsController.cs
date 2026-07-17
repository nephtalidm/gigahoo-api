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
        [FromQuery] string? status = null,
        [FromQuery] byte? typeId = null)
    {
        var accountId = GetAccountId();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Conversations
            .Include(c => c.Language)
            .Where(c => c.AccountId == accountId);

        // Channel filter (ConversationType seed: 1 = Phone Call). The dashboard's Call History
        // passes typeId=1; future message receptionists (SMS/WhatsApp) query their own type.
        if (typeId is not null)
            query = query.Where(c => c.ConversationTypeId == typeId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.ConversationStatus!.Name == status);

        var totalCount = await query.CountAsync();

        var conversations = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationResponse(
                c.ConversationId,
                c.CallerName,
                c.CallerPhoneNumber,
                c.CreatedDate,
                c.DurationSeconds,
                c.Language != null ? c.Language.Name : "English",
                c.Summary,
                c.Address,
                c.IsEmergency,
                c.ConversationStatus!.Name
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
            .Include(c => c.ConversationStatus)
            .FirstOrDefaultAsync(c => c.ConversationId == id && c.AccountId == accountId);

        if (conversation is null) return NotFound();

        return Ok(new ConversationResponse(
            conversation.ConversationId,
            conversation.CallerName,
            conversation.CallerPhoneNumber,
            conversation.CreatedDate,
            conversation.DurationSeconds,
            conversation.Language?.Name ?? "English",
            conversation.Summary,
            conversation.Address,
            conversation.IsEmergency,
            conversation.ConversationStatus!.Name
        ));
    }
}
