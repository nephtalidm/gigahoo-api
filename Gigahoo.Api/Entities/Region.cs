namespace Gigahoo.Api.Entities;

public class Region
{
    public short RegionId { get; set; }
    public short CountryId { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;

    public Country Country { get; set; } = null!;
}
