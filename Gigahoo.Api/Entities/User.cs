namespace Gigahoo.Api.Entities;

public class User
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NormalizedPhone { get; set; }
    public string? PasswordHash { get; set; }
    public string? GoogleSubjectId { get; set; }
    public string? DisplayName { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public bool IsPhoneConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsDisabled { get; set; }

    public Account? Account { get; set; }
}
