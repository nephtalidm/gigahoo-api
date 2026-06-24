namespace Gigahoo.Api.Dtos;

public record CountryResponse(short Id, string Name, string Code, string DialCode, string? Flag);

public record RegionResponse(short Id, string Name, string Code);

public record BusinessCategoryResponse(byte Id, string Name);

public record LanguageResponse(byte Id, string Name);

public record DashboardOverviewResponse(
    string Plan,
    int IncludedMinutes,
    int MinutesUsed,
    int RemainingMinutes,
    string BillingPeriod,
    int ConversationsAnswered,
    double AvgConversationDurationSeconds,
    List<ConversationResponse> RecentConversations
);
