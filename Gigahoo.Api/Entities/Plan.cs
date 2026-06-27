namespace Gigahoo.Api.Entities;

public class Plan
{
    public byte Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal PriceMonthly { get; set; }
    public int IncludedMinutes { get; set; }
    public bool HasOptionalFeatures { get; set; }
    public int? MaxConcurrentCalls { get; set; }      // simultaneous live calls allowed (NULL = unlimited; Free/Starter = 1)
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }             // ordering for upgrade/downgrade + card order (data-driven, not hardcoded)
    public ICollection<PlanPrice> Prices { get; set; } = [];
}
