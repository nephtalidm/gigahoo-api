using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Services;

/// <summary>
/// Number lifecycle service. Carrier operations are delegated to the configured
/// <see cref="ITelephonyProvider"/>; this service owns the DB pool / assignment
/// bookkeeping. (Name kept as ITwilioService for backwards compatibility with
/// existing callers; the actual carrier is provider-selectable via config.)
/// </summary>
public interface ITwilioService
{
    Task<PurchasedPhoneNumber?> PurchasePhoneNumberAsync(string countryCode);
    Task ConfigureWebhookAsync(string phoneNumberSid, string webhookUrl);
    Task ReleasePhoneNumberAsync(string phoneNumberSid);
    Task<Entities.PhoneNumber?> GetAvailableNumberAsync(string countryCode);
    Task AssignNumberToAccountAsync(Entities.PhoneNumber phoneNumber, Guid accountId);
    Task ReleaseNumberFromAccountAsync(Guid accountId);
}

public class TwilioService(GigahooDbContext db, ITelephonyProvider telephony) : ITwilioService
{
    public Task<PurchasedPhoneNumber?> PurchasePhoneNumberAsync(string countryCode)
        => telephony.PurchaseAsync(countryCode, areaCode: null);

    public Task ConfigureWebhookAsync(string phoneNumberSid, string webhookUrl)
        => telephony.ConfigureVoiceWebhookAsync(phoneNumberSid, webhookUrl);

    public Task ReleasePhoneNumberAsync(string phoneNumberSid)
        => telephony.ReleaseAsync(phoneNumberSid);

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

    /// <summary>
    /// Release the account's number. Calls the carrier to actually de-provision the
    /// number, then flips the pool record back to Available.
    /// </summary>
    public async Task ReleaseNumberFromAccountAsync(Guid accountId)
    {
        var phoneNumber = await db.PhoneNumbers
            .FirstOrDefaultAsync(p => p.AssignedAccountId == accountId && p.Status == Entities.PhoneNumberStatus.Assigned);

        if (phoneNumber != null)
        {
            // Actually de-provision at the carrier before flipping DB status.
            await telephony.ReleaseAsync(phoneNumber.Sid);

            phoneNumber.Status = Entities.PhoneNumberStatus.Released;
            phoneNumber.AssignedAccountId = null;
            phoneNumber.ReleasedAt = DateTime.UtcNow;
            phoneNumber.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }
    }
}
