namespace Gigahoo.Api.Entities;

public class FeatureSettings
{
    public Guid AccountId { get; set; }
    public bool AnswerQuestions { get; set; }
    public string? ServicesInfo { get; set; }
    public bool ServeArea { get; set; }
    public int DistanceKm { get; set; } = 50;
    public bool QuoteInspection { get; set; }
    public decimal PricePerKm { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Account Account { get; set; } = null!;
}
