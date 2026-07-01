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
    Task<Entities.PhoneNumber> AddPurchasedNumberToPoolAsync(PurchasedPhoneNumber purchased, string countryCode);
    Task AssignNumberToAccountAsync(Entities.PhoneNumber phoneNumber, Guid accountId);
    Task ReleaseNumberFromAccountAsync(Guid accountId);
}

public class TwilioService(GigahooDbContext db, ITelephonyProvider telephony, IConfiguration config) : ITwilioService
{
    public Task<PurchasedPhoneNumber?> PurchasePhoneNumberAsync(string countryCode)
        => telephony.PurchaseAsync(countryCode, areaCode: null);

    public Task ConfigureWebhookAsync(string phoneNumberSid, string webhookUrl)
        => telephony.ConfigureVoiceWebhookAsync(phoneNumberSid, webhookUrl);

    public Task ReleasePhoneNumberAsync(string phoneNumberSid)
        => telephony.ReleaseAsync(phoneNumberSid);

    public async Task<Entities.PhoneNumber?> GetAvailableNumberAsync(string countryCode)
    {
        var resolved = await ResolveCountryIdAsync(countryCode);
        if (resolved is not short countryId) return null;

        // Testing: when reuse SIDs are configured, reuse the one matching this
        // country (detach from any prior account, mark Available) instead of buying.
        var reuseSids = (config["Telephony:ReuseNumberSids"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (reuseSids.Length > 0)
        {
            var reuse = await db.PhoneNumbers
                .FirstOrDefaultAsync(p => reuseSids.Contains(p.Sid) && p.CountryId == countryId);
            if (reuse is not null)
            {
                if (reuse.AssignedAccountId is Guid prevId)
                {
                    var prev = await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == prevId);
                    if (prev is not null) { prev.PhoneNumberSid = null; prev.ForwardingPhone = null; }
                }
                reuse.PhoneNumberStatusId = (byte)Entities.PhoneNumberStatusId.Available;
                reuse.AssignedAccountId = null;
                await db.SaveChangesAsync();
            }
            return reuse;
        }

        // Pool numbers must match the active carrier so we never hand out a Twilio
        // number while Telnyx is the configured provider (or vice-versa).
        var providerId = await ResolveActiveProviderIdAsync();

        // First, try to find an available number in the pool for this country + carrier.
        var availableNumber = await db.PhoneNumbers
            .Where(p => p.CountryId == countryId && p.ProviderId == providerId && p.PhoneNumberStatusId == (byte)Entities.PhoneNumberStatusId.Available)
            .OrderBy(p => p.PurchasedAt)
            .FirstOrDefaultAsync();

        return availableNumber;
    }

    // Adds a freshly-purchased carrier number to the pool, resolving the Country
    // and active-carrier Provider foreign keys from their codes.
    public async Task<Entities.PhoneNumber> AddPurchasedNumberToPoolAsync(PurchasedPhoneNumber purchased, string countryCode)
    {
        var number = new Entities.PhoneNumber
        {
            Sid = purchased.Sid,
            Number = purchased.PhoneNumber,
            CountryId = await ResolveCountryIdAsync(countryCode)
                ?? throw new InvalidOperationException($"Unknown country code '{countryCode}'"),
            ProviderId = await ResolveActiveProviderIdAsync(),
            PhoneNumberStatusId = (byte)Entities.PhoneNumberStatusId.Available,
            MonthlyCost = 1.15m,
            PurchasedAt = DateTime.UtcNow
        };
        db.PhoneNumbers.Add(number);
        await db.SaveChangesAsync();
        return number;
    }

    private async Task<short?> ResolveCountryIdAsync(string countryCode)
        => (await db.Countries.FirstOrDefaultAsync(c => c.Code == countryCode))?.CountryId;

    private async Task<int> ResolveActiveProviderIdAsync()
        => (await db.Providers.FirstAsync(p =>
                p.Code == telephony.ProviderName && p.ProviderTypeId == (byte)ProviderTypeId.Phone)).ProviderId;

    public async Task AssignNumberToAccountAsync(Entities.PhoneNumber phoneNumber, Guid accountId)
    {
        phoneNumber.PhoneNumberStatusId = (byte)Entities.PhoneNumberStatusId.Assigned;
        phoneNumber.AssignedAccountId = accountId;
        phoneNumber.AssignedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Release the account's number. Calls the carrier to actually de-provision the
    /// number, then flips the pool record back to Available.
    /// </summary>
    public async Task ReleaseNumberFromAccountAsync(Guid accountId)
    {
        var phoneNumber = await db.PhoneNumbers
            .FirstOrDefaultAsync(p => p.AssignedAccountId == accountId && p.PhoneNumberStatusId == (byte)Entities.PhoneNumberStatusId.Assigned);

        if (phoneNumber != null)
        {
            var reuseSids = (config["Telephony:ReuseNumberSids"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (reuseSids.Contains(phoneNumber.Sid))
            {
                // Reusable test number — free it but never de-provision at the carrier.
                phoneNumber.PhoneNumberStatusId = (byte)Entities.PhoneNumberStatusId.Available;
            }
            else
            {
                // Actually de-provision at the carrier before flipping DB status.
                await telephony.ReleaseAsync(phoneNumber.Sid);
                phoneNumber.PhoneNumberStatusId = (byte)Entities.PhoneNumberStatusId.Released;
            }
            phoneNumber.AssignedAccountId = null;

            await db.SaveChangesAsync();
        }
    }
}
