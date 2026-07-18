using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Services;

public interface IPhoneNumberCleanupService
{
    Task CleanupInactiveNumbersAsync(int inactiveDays = 30);
}

/// <summary>
/// The ONLY automated number lifecycle today: returning abandoned unpaid reservations to the
/// pool. The reserve-then-charge flow assigns a number before payment; if the customer abandons
/// the card step, the account is parked on the Free plan while still HOLDING the unpaid
/// subscription id — that pairing (free plan + subscription id) is the abandonment signature.
/// A legitimate Free account has a number and NO subscription id and must never be touched
/// (an earlier version keyed on "no subscription id" and would have stripped every free
/// customer's number). After 24h the number returns to the pool as a pure DB detach. Numbers
/// are NEVER de-provisioned at the carrier by automation — purchased numbers are paid
/// inventory; deliberate carrier release belongs to a future, explicit grace-period feature.
/// </summary>
public class PhoneNumberCleanupService(
    GigahooDbContext db,
    ILogger<PhoneNumberCleanupService> logger) : IPhoneNumberCleanupService
{
    public async Task CleanupInactiveNumbersAsync(int inactiveDays = 30)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        var freePlanIds = await db.Plans
            .Where(p => p.PriceMonthly == 0)
            .Select(p => p.PlanId)
            .ToListAsync();

        var abandoned = await db.PhoneNumbers
            .Where(p => p.PhoneNumberStatusId == (byte)PhoneNumberStatusId.Assigned)
            .Where(p => p.AssignedAt != null && p.AssignedAt < cutoff)
            .Join(db.Accounts,
                p => p.AssignedAccountId,
                a => a.AccountId,
                (p, a) => new { Phone = p, Account = a })
            .Where(x => x.Account.AssignedPhoneNumberId != null
                && x.Account.StripeSubscriptionId != null
                && freePlanIds.Contains(x.Account.PlanId))
            .ToListAsync();

        if (abandoned.Count == 0) return;
        logger.LogInformation("Returning {Count} abandoned phone reservations to the pool", abandoned.Count);

        foreach (var x in abandoned)
        {
            x.Phone.AssignedAccountId = null;
            x.Phone.PhoneNumberStatusId = (byte)PhoneNumberStatusId.Available;
            x.Phone.AssignedAt = null;
            x.Account.AssignedPhoneNumberId = null;
            // The unpaid subscription id is stale bookkeeping — clear it so the account is a
            // clean Free account (the incomplete subscription expires on Stripe's side).
            x.Account.StripeSubscriptionId = null;
            x.Account.UpdatedAt = DateTime.UtcNow;
            logger.LogInformation("Returned abandoned reservation {Number} from account {AccountId}", x.Phone.Number, x.Account.AccountId);
        }

        await db.SaveChangesAsync();
    }
}
