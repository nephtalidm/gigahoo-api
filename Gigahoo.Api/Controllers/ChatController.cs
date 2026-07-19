using Gigahoo.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Gigahoo.Api.Controllers;

/// <summary>
/// Homepage help chat: answers visitor questions about Gigahoo using qwen-flash (the same
/// cheap model the voice stack's judges run on). Anonymous but rate-limited per IP, hard-scoped
/// to Gigahoo topics, prices injected LIVE from PlanPrice so it can never quote a stale number.
/// Conversations are not stored.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[EnableRateLimiting("chat")]
public class ChatController(
    GigahooDbContext db,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<ChatController> logger) : ControllerBase
{
    private const string ChatUrl = "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request)
    {
        var key = config["DASHSCOPE_API_KEY"];
        if (string.IsNullOrEmpty(key))
            return StatusCode(503, new { error = "Chat is unavailable right now." });

        // Live plan facts — data-driven, never hardcoded into the prompt.
        var plans = await db.Plans.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).ToListAsync();
        var prices = await db.PlanPrices.Where(pp => pp.IsActive).ToListAsync();
        var priceLines = string.Join("\n", plans.Select(p =>
        {
            var perCurrency = prices.Where(x => x.PlanId == p.PlanId)
                .OrderBy(x => x.Currency)
                .Select(x => $"{x.Currency} {x.Amount:0}");
            var amount = p.PriceMonthly == 0 ? "Free" : string.Join(" / ", perCurrency) + " per month";
            return $"- {p.Name}: {amount}, includes {p.IncludedMinutes} call minutes per month";
        }));

        var system = $"""
            You are the friendly website assistant for Gigahoo (https://gigahoo.ai) — an AI phone
            receptionist for small businesses (plumbers, salons, contractors, clinics and similar).

            FACTS YOU MAY USE:
            - Gigahoo answers a business's phone calls 24/7 with a natural AI voice receptionist.
            - It collects the caller's name, callback number, address and reason for calling, and
              detects emergencies. The owner gets a call summary by email and SMS, and the full
              conversation transcript in the dashboard.
            - It speaks many languages (English, Spanish, French, Japanese and more) and can switch
              language and voice naturally mid-call when the caller switches.
            - Every account gets a dedicated business phone number (US and Canada). Owners simply
              forward their existing business line to it.
            - Setup takes minutes: sign up at gigahoo.ai/signup. There is a free plan to try it.
            - Plans and monthly prices:
            {priceLines}
            - Billing is by card (monthly, cancel anytime); every payment gets an emailed receipt
              with the invoice PDF attached. Currently available in the United States and Canada.
            - The dashboard includes call history with transcripts, plan and billing management,
              voice and greeting settings, and control over which caller details are collected.
            - Human contact: contact@gigahoo.ai

            RULES:
            - Only answer questions about Gigahoo, its features, pricing and signup. For anything
              else, politely say you can only help with Gigahoo and point to contact@gigahoo.ai.
            - Reply in the language the visitor writes in.
            - Be concise: 2-4 short sentences. Never invent features, prices or availability that
              are not in the facts above. If unsure, say so and point to contact@gigahoo.ai.
            """;

        var messages = new List<object> { new { role = "system", content = system } };
        foreach (var m in request.Messages.TakeLast(10))
        {
            var role = m.Role == "assistant" ? "assistant" : "user";
            var content = m.Content.Length > 1000 ? m.Content[..1000] : m.Content;
            messages.Add(new { role, content });
        }

        try
        {
            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            using var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            req.Content = JsonContent.Create(new { model = "qwen-flash", messages, max_tokens = 500, temperature = 0.3 });
            var res = await http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogError("Chat model call failed: {Status} {Body}", res.StatusCode, await res.Content.ReadAsStringAsync());
                return StatusCode(502, new { error = "Chat is unavailable right now." });
            }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return Ok(new { reply });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat request failed");
            return StatusCode(502, new { error = "Chat is unavailable right now." });
        }
    }
}

public record ChatRequestMessage
{
    [Required, MaxLength(20)]
    public string Role { get; init; } = "user";

    [Required, MaxLength(4000)]
    public string Content { get; init; } = default!;
}

public record ChatRequest
{
    [Required, MaxLength(20)]
    public List<ChatRequestMessage> Messages { get; init; } = default!;
}
