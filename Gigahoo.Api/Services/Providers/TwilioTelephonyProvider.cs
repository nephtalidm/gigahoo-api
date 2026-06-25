using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;

namespace Gigahoo.Api.Services.Providers;

/// <summary>
/// Twilio carrier implementation of <see cref="ITelephonyProvider"/>.
/// Contains the real Twilio provisioning / de-provisioning logic (formerly in TwilioService).
/// </summary>
public class TwilioTelephonyProvider(IConfiguration config, ILogger<TwilioTelephonyProvider> logger) : ITelephonyProvider
{
    private readonly string _accountSid = config["Twilio:AccountSid"]!;
    private readonly string _authToken = config["Twilio:AuthToken"]!;

    public string ProviderName => "twilio";

    public async Task<IReadOnlyList<AvailablePhoneNumber>> SearchAvailableAsync(string country, string? areaCode)
    {
        TwilioClient.Init(_accountSid, _authToken);

        var available = await LocalResource.ReadAsync(
            pathCountryCode: country,
            areaCode: string.IsNullOrWhiteSpace(areaCode) ? null : int.TryParse(areaCode, out var ac) ? ac : null,
            limit: 10
        );

        return available
            .Select(n => new AvailablePhoneNumber(n.PhoneNumber.ToString(), country))
            .ToList();
    }

    public async Task<PurchasedPhoneNumber?> PurchaseAsync(string country, string? areaCode)
    {
        try
        {
            TwilioClient.Init(_accountSid, _authToken);

            var available = await SearchAvailableAsync(country, areaCode);
            if (available.Count == 0) return null;

            var selected = available[0];

            var purchased = await IncomingPhoneNumberResource.CreateAsync(
                phoneNumber: new Twilio.Types.PhoneNumber(selected.PhoneNumber),
                voiceUrl: new Uri("https://example.com/voice"), // Temporary, updated by ConfigureVoiceWebhookAsync
                voiceMethod: "POST"
            );

            return new PurchasedPhoneNumber(purchased.Sid, purchased.PhoneNumber.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purchasing Twilio phone number for {Country}", country);
            return null;
        }
    }

    public async Task ConfigureVoiceWebhookAsync(string sid, string webhookUrl)
    {
        TwilioClient.Init(_accountSid, _authToken);

        await IncomingPhoneNumberResource.UpdateAsync(
            pathSid: sid,
            voiceUrl: new Uri(webhookUrl),
            voiceMethod: "POST"
        );
    }

    public async Task ReleaseAsync(string sid)
    {
        TwilioClient.Init(_accountSid, _authToken);

        await IncomingPhoneNumberResource.DeleteAsync(pathSid: sid);
    }
}
