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
                await twilio.ReleaseNumberFromAccountAsync(account.Id);

                // Clear the phone number reference from account
                account.PhoneNumberSid = null;
                account.TelephonyProvider = null;
                account.UpdatedAt = DateTime.UtcNow;

                logger.LogInformation(
                    "Released phone number from inactive account {AccountId} (last activity: {LastActivity})",
                    account.Id,
                    account.Conversations.Max(c => (DateTime?)c.DateTimeUtc) ?? account.CreatedAt);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to release phone number from account {AccountId}", account.Id);
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Phone number cleanup completed");
    }
}
