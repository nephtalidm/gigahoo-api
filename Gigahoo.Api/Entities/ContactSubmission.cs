namespace Gigahoo.Api.Entities;

public class ContactSubmission
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
