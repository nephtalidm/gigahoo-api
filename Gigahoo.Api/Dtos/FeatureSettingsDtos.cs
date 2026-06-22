namespace Gigahoo.Api.Dtos;

public record UpdateFeatureSettingsRequest(
    bool AnswerQuestions,
    string? ServicesInfo,
    bool ServeArea,
    int DistanceKm,
    bool QuoteInspection,
    decimal PricePerKm
);

public record FeatureSettingsResponse(
    bool AnswerQuestions,
    string? ServicesInfo,
    bool ServeArea,
    int DistanceKm,
    bool QuoteInspection,
    decimal PricePerKm
);
