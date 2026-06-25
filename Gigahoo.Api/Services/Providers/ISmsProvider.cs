namespace Gigahoo.Api.Services.Providers;

/// <summary>
/// General-purpose outbound SMS, provider-agnostic.
/// </summary>
public interface ISmsProvider
{
    Task SendAsync(string toE164, string body);
}
