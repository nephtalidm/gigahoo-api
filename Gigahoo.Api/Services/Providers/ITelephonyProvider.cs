namespace Gigahoo.Api.Services.Providers;

/// <summary>
/// A phone number available for purchase from a telephony carrier.
/// </summary>
public record AvailablePhoneNumber(string PhoneNumber, string CountryCode);

/// <summary>
/// The result of purchasing a phone number from a carrier.
/// </summary>
public record PurchasedPhoneNumber(string Sid, string PhoneNumber);

/// <summary>
/// Carrier-level telephony operations. Implementations talk to a real carrier
/// (Twilio, Telnyx, ...) and perform actual provisioning / de-provisioning.
/// DB bookkeeping (pool, assignment status) lives outside this abstraction.
/// </summary>
public interface ITelephonyProvider
{
    /// <summary>Provider key as stored on entities (e.g. "twilio", "telnyx").</summary>
    string ProviderName { get; }

    Task<IReadOnlyList<AvailablePhoneNumber>> SearchAvailableAsync(string country, string? areaCode);

    /// <summary>Purchase a number for the given country. Returns null if none could be purchased.</summary>
    Task<PurchasedPhoneNumber?> PurchaseAsync(string country, string? areaCode);

    Task ConfigureVoiceWebhookAsync(string sid, string webhookUrl);

    /// <summary>Actually release / de-provision the number at the carrier.</summary>
    Task ReleaseAsync(string sid);
}
