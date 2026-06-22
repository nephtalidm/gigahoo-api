using System.ComponentModel.DataAnnotations;

namespace Gigahoo.Api.Dtos;

public record UpdateFeatureSettingsRequest
{
    public bool AnswerQuestions { get; init; }

    [MaxLength(2000)]
    public string? ServicesInfo { get; init; }

    [MaxLength(500)]
    public string? ServiceAreas { get; init; }

    [MaxLength(500)]
    public string? BusinessHours { get; init; }

    [MaxLength(500)]
    public string? EmergencyAvailability { get; init; }

    [MaxLength(2000)]
    public string? PricingPolicy { get; init; }

    [MaxLength(2000)]
    public string? WarrantyPolicy { get; init; }

    [MaxLength(5000)]
    public string? FrequentlyAskedQuestions { get; init; }

    [MaxLength(2000)]
    public string? AdditionalBusinessInfo { get; init; }

    public bool ServeArea { get; init; }

    [Range(1, 1000)]
    public int DistanceKm { get; init; }

    public bool QuoteInspection { get; init; }

    [Range(0, 10000)]
    public decimal PricePerKm { get; init; }
}

public record FeatureSettingsResponse(
    bool AnswerQuestions,
    string? ServicesInfo,
    string? ServiceAreas,
    string? BusinessHours,
    string? EmergencyAvailability,
    string? PricingPolicy,
    string? WarrantyPolicy,
    string? FrequentlyAskedQuestions,
    string? AdditionalBusinessInfo,
    bool ServeArea,
    int DistanceKm,
    bool QuoteInspection,
    decimal PricePerKm
);
