namespace Gigahoo.Api.Dtos;

public record ChangePlanRequest(byte PlanId);

public record PlanResponse(
    byte Id,
    string Name,
    decimal PriceMonthly,
    int IncludedMinutes,
    bool HasOptionalFeatures,
    int DisplayOrder,
    List<string> Features
);

public record BillingSummaryResponse(
    string Plan,
    int IncludedMinutes,
    int MinutesUsed,
    int RemainingMinutes,
    string BillingPeriod,
    decimal UsagePercent
);

public record InvoiceResponse(
    Guid Id,
    string InvoiceNumber,
    DateTime DateUtc,
    decimal Amount,
    string Currency,
    string Status,
    string? PdfUrl
);
