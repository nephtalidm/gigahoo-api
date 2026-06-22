namespace Gigahoo.Api.Entities;

public class RefreshToken
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public bool IsRevoked => RevokedAt is not null;

    public User User { get; set; } = null!;
}
