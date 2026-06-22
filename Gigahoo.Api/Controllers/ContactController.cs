using Gigahoo.Api.Data;
using Gigahoo.Api.Dtos;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gigahoo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController(
    GigahooDbContext db,
    IEmailService email) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] ContactRequest request)
    {
        var submission = new ContactSubmission
        {
            Name = request.Name,
            Email = request.Email,
            Subject = request.Subject,
            Message = request.Message,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.UtcNow,
        };

        db.ContactSubmissions.Add(submission);
        await db.SaveChangesAsync();

        await email.SendContactNotificationAsync(request.Name, request.Email, request.Subject, request.Message);

        return Ok(new { message = "Message sent successfully" });
    }
}
