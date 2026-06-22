namespace Gigahoo.Api.Entities;

public class Country
{
    public short Id { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string DialCode { get; set; } = null!;
    public string? Flag { get; set; }
}
