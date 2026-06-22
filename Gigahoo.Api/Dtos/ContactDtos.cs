using System.ComponentModel.DataAnnotations;

namespace Gigahoo.Api.Dtos;

public record ContactRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = default!;

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = default!;

    [Required, MaxLength(200)]
    public string Subject { get; init; } = default!;

    [Required, MaxLength(5000)]
    public string Message { get; init; } = default!;
}
