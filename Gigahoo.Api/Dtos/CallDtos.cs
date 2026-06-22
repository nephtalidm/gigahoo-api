namespace Gigahoo.Api.Dtos;

public record CallResponse(
    Guid Id,
    string? CallerName,
    string CallerPhone,
    DateTime DateTimeUtc,
    int DurationSeconds,
    string Language,
    string? Summary,
    string Status,
    List<CollectedInfoDto> CollectedInfo
);

public record CollectedInfoDto(string Label, string Value);

public record CallsPageResponse(
    List<CallResponse> Items,
    int TotalCount,
    int Page,
    int PageSize
);
