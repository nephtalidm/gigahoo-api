namespace Gigahoo.Api.Dtos;

public record ConversationResponse(
    Guid Id,
    string? CallerName,
    string CallerPhoneNumber,
    DateTime DateTimeUtc,
    int DurationSeconds,
    string Language,
    string? Summary,
    string? Address,
    string Status
);

public record ConversationsPageResponse(
    List<ConversationResponse> Items,
    int TotalCount,
    int Page,
    int PageSize
);
