namespace Gigahoo.Api.Entities;

public class Plan
{
    public byte Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal PriceMonthly { get; set; }
    public int IncludedMinutes { get; set; }
    public bool HasOptionalFeatures { get; set; }
    public bool IsActive { get; set; }
}
