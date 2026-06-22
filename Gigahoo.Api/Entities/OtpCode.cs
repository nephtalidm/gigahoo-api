namespace Gigahoo.Api.Entities;

public class OtpCode
{
    public long Id { get; set; }
    public string Identifier { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Type { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Attempts { get; set; }
}
