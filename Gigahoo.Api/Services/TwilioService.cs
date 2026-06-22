using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;
using TwilioPhoneNumber = Twilio.Types.PhoneNumber;

namespace Gigahoo.Api.Services;

public interface ITwilioService
{
    Task<string?> PurchasePhoneNumberAsync(string countryCode);
    Task ConfigureWebhookAsync(string phoneNumberSid, string webhookUrl);
    Task ReleasePhoneNumberAsync(string phoneNumberSid);
    Task<Entities.PhoneNumber?> GetAvailableNumberAsync(string countryCode);
    Task AssignNumberToAccountAsync(Entities.PhoneNumber phoneNumber, Guid accountId);
    Task ReleaseNumberFromAccountAsync(Guid accountId);
}

public class TwilioService(IConfiguration config, GigahooDbContext db) : ITwilioService
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

    public async Task<Entities.PhoneNumber?> GetAvailableNumberAsync(string countryCode)
    {
        // First, try to find an available number in the pool for this country
        var availableNumber = await db.PhoneNumbers
            .Where(p => p.CountryCode == countryCode && p.Status == Entities.PhoneNumberStatus.Available)
            .OrderBy(p => p.PurchasedAt)
            .FirstOrDefaultAsync();

        return availableNumber;
    }

    public async Task AssignNumberToAccountAsync(Entities.PhoneNumber phoneNumber, Guid accountId)
    {
        phoneNumber.Status = Entities.PhoneNumberStatus.Assigned;
        phoneNumber.AssignedAccountId = accountId;
        phoneNumber.AssignedAt = DateTime.UtcNow;
        phoneNumber.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task ReleaseNumberFromAccountAsync(Guid accountId)
    {
        var phoneNumber = await db.PhoneNumbers
            .FirstOrDefaultAsync(p => p.AssignedAccountId == accountId && p.Status == Entities.PhoneNumberStatus.Assigned);

        if (phoneNumber != null)
        {
            phoneNumber.Status = Entities.PhoneNumberStatus.Available;
            phoneNumber.AssignedAccountId = null;
            phoneNumber.ReleasedAt = DateTime.UtcNow;
            phoneNumber.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }
    }
}
