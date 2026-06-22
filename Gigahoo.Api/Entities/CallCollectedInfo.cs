namespace Gigahoo.Api.Entities;

public class CallCollectedInfo
{
    public long Id { get; set; }
    public Guid CallId { get; set; }
    public string Label { get; set; } = null!;
    public string Value { get; set; } = null!;

    public Call Call { get; set; } = null!;
}
