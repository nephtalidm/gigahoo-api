namespace Gigahoo.Api.Dtos;

public record ContactRequest(
    string Name,
    string Email,
    string Subject,
    string Message
);
