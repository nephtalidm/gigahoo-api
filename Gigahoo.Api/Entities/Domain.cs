namespace Gigahoo.Api.Entities;

public class Domain
{
    public string Host { get; set; } = null!;     // Regional domain host, e.g. "gigahoo.ca"
    public string? CountryCode { get; set; }       // ISO-2 forced market, or NULL = geo-detect
}
