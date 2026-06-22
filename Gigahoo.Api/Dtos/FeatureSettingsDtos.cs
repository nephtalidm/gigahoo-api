using System.ComponentModel.DataAnnotations;

namespace Gigahoo.Api.Dtos;

public record UpdateFeatureSettingsRequest
{
    public bool AnswerQuestions { get; init; }

    [MaxLength(2000)]
    public string? ServicesInfo { get; init; }

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
    bool ServeArea,
    int DistanceKm,
    bool QuoteInspection,
    decimal PricePerKm
);
