namespace Gigahoo.Api.Entities;

// General website settings as simple key/value pairs.
public class Setting
{
    public string SettingKey { get; set; } = null!;
    public string? SettingValue { get; set; }
}
