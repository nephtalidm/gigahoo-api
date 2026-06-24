namespace Gigahoo.Api.Dtos;

public record ConversationResponse(
    Guid Id,
    string? CallerName,
    string CallerPhone,
    DateTime DateTimeUtc,
    int DurationSeconds,
    string Language,
    string? Summary,
    string Status
);

public record ConversationsPageResponse(
    List<ConversationResponse> Items,
    int TotalCount,
    int Page,
    int PageSize
);
