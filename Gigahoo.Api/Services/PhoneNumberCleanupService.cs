using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Gigahoo.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Services;

public interface IPhoneNumberCleanupService
{
    Task CleanupInactiveNumbersAsync(int inactiveDays = 30);
}

public class PhoneNumberCleanupService(
    GigahooDbContext db,
    ITwilioService twilio,
    ILogger<PhoneNumberCleanupService> logger) : IPhoneNumberCleanupService
{
    public async Task CleanupInactiveNumbersAsync(int inactiveDays = 30)
    {
        logger.LogInformation("Starting phone number cleanup for accounts inactive for {Days} days", inactiveDays);

        var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDays);

        // Find accounts with phone numbers that have no recent conversations
        var inactiveAccounts = await db.Accounts
            .Include(a => a.Conversations)
            .Where(a => a.PhoneNumberSid != null)
            .Where(a => a.Conversations.All(c => c.DateTimeUtc < cutoffDate) || !a.Conversations.Any())
            .ToListAsync();

        logger.LogInformation("Found {Count} inactive accounts with phone numbers", inactiveAccounts.Count);

        foreach (var account in inactiveAccounts)
        {
            try
            {
                // Send warning email first (optional - you might want to skip this for automated cleanup)
                // await SendWarningEmailAsync(account, inactiveDays);

                // Release the phone number back to pool
                await twilio.ReleaseNumberFromAccountAsync(account.AccountId);

                // Clear the phone number reference from account
                account.PhoneNumberSid = null;
                account.TelephonyProvider = null;
                account.UpdatedAt = DateTime.UtcNow;

                logger.LogInformation(
                    "Released phone number from inactive account {AccountId} (last activity: {LastActivity})",
                    account.AccountId,
                    account.Conversations.Max(c => (DateTime?)c.DateTimeUtc) ?? account.CreatedAt);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to release phone number from account {AccountId}", account.AccountId);
            }
        }

        await ReleaseAbandonedReservationsAsync();

        await db.SaveChangesAsync();
        logger.LogInformation("Phone number cleanup completed");
    }

    /// <summary>
    /// Release numbers that were RESERVED during checkout but never paid for. The
    /// reserve-then-charge flow assigns a number before payment; if the customer
    /// abandons checkout, the account ends up with a PhoneNumberSid but no
    /// StripeSubscriptionId. After 24h, hand the number back to the pool.
    /// (Free accounts never reserve a number, so this only catches abandoned paid checkouts.)
    /// </summary>
    private async Task ReleaseAbandonedReservationsAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        var abandoned = await db.PhoneNumbers
            .Where(p => p.Status == Entities.PhoneNumberStatus.Assigned)
            .Where(p => p.AssignedAt != null && p.AssignedAt < cutoff)
            .Join(db.Accounts,
                p => p.AssignedAccountId,
                a => a.AccountId,
                (p, a) => new { Phone = p, Account = a })
            .Where(x => x.Account.PhoneNumberSid != null && x.Account.StripeSubscriptionId == null)
            .Select(x => x.Account)
            .ToListAsync();

        logger.LogInformation("Found {Count} abandoned phone reservations to release", abandoned.Count);

        foreach (var account in abandoned)
        {
            try
            {
                await twilio.ReleaseNumberFromAccountAsync(account.AccountId);

                account.PhoneNumberSid = null;
                account.TelephonyProvider = null;
                account.ForwardingPhone = null;
                account.UpdatedAt = DateTime.UtcNow;

                logger.LogInformation("Released abandoned phone reservation for account {AccountId}", account.AccountId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to release abandoned phone reservation for account {AccountId}", account.AccountId);
            }
        }
    }
}
