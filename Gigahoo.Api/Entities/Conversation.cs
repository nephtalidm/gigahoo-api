namespace Gigahoo.Api.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string? CallerName { get; set; }
    public string CallerPhone { get; set; } = null!;
    public DateTime DateTimeUtc { get; set; }
    public int DurationSeconds { get; set; }
    public byte? LanguageId { get; set; }
    public string? Summary { get; set; }
    public string Status { get; set; } = "Missed";
    public DateTime CreatedAt { get; set; }

    public Account Account { get; set; } = null!;
    public Language? Language { get; set; }
}
