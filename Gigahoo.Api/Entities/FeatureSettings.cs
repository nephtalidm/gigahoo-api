namespace Gigahoo.Api.Entities;

public class FeatureSettings
{
    public Guid AccountId { get; set; }
    public bool AnswerQuestions { get; set; }
    public string? ServicesInfo { get; set; }
    public string? ServiceAreas { get; set; }
    public string? BusinessHours { get; set; }
    public string? EmergencyAvailability { get; set; }
    public string? PricingPolicy { get; set; }
    public string? WarrantyPolicy { get; set; }
    public string? FrequentlyAskedQuestions { get; set; }
    public string? AdditionalBusinessInfo { get; set; }
    public bool ServeArea { get; set; }
    public int DistanceKm { get; set; } = 50;
    public bool QuoteInspection { get; set; }
    public decimal PricePerKm { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Account Account { get; set; } = null!;
}
