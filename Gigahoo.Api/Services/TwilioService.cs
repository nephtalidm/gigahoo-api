using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;
using Twilio.Types;

namespace Gigahoo.Api.Services;

public interface ITwilioService
{
    Task<string?> PurchasePhoneNumberAsync(string countryCode);
    Task ConfigureWebhookAsync(string phoneNumberSid, string webhookUrl);
    Task ReleasePhoneNumberAsync(string phoneNumberSid);
}

public class TwilioService(IConfiguration config) : ITwilioService
{
    private readonly string _accountSid = config["Twilio:AccountSid"]!;
    private readonly string _authToken = config["Twilio:AuthToken"]!;

    public async Task<string?> PurchasePhoneNumberAsync(string countryCode)
    {
        try
        {
            TwilioClient.Init(_accountSid, _authToken);

            // Search for available phone numbers
            var availableNumbers = await LocalResource.ReadAsync(
                pathCountryCode: countryCode,
                limit: 10
            );

            if (!availableNumbers.Any())
            {
                return null;
            }

            // Pick the first available number
            var selectedNumber = availableNumbers.First();

            // Purchase the number
            var purchasedNumber = await IncomingPhoneNumberResource.CreateAsync(
                phoneNumber: selectedNumber.PhoneNumber,
                voiceUrl: new Uri("https://example.com/voice"), // Temporary, will be updated
                voiceMethod: "POST"
            );

            return purchasedNumber.Sid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error purchasing phone number: {ex.Message}");
            return null;
        }
    }

    public async Task ConfigureWebhookAsync(string phoneNumberSid, string webhookUrl)
    {
        try
        {
            TwilioClient.Init(_accountSid, _authToken);

            await IncomingPhoneNumberResource.UpdateAsync(
                pathSid: phoneNumberSid,
                voiceUrl: new Uri(webhookUrl),
                voiceMethod: "POST"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error configuring webhook: {ex.Message}");
            throw;
        }
    }

    public async Task ReleasePhoneNumberAsync(string phoneNumberSid)
    {
        try
        {
            TwilioClient.Init(_accountSid, _authToken);

            await IncomingPhoneNumberResource.DeleteAsync(pathSid: phoneNumberSid);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error releasing phone number: {ex.Message}");
            throw;
        }
    }
}
