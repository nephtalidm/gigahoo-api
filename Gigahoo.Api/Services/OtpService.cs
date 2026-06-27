using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Services;

public interface IOtpService
{
    Task<string> GenerateAndStoreAsync(string identifier, string type, TimeSpan expiry);
    Task<bool> VerifyAsync(string identifier, string type, string code);
    /// Seconds the caller must still wait before another code of this type may be
    /// sent for this identifier (0 = may send now). Enforces a minimum send interval.
    Task<int> SecondsUntilResendAsync(string identifier, string type, TimeSpan minInterval);
}

public class OtpService(GigahooDbContext db) : IOtpService
{
    public async Task<string> GenerateAndStoreAsync(string identifier, string type, TimeSpan expiry)
    {
        var code = Random.Shared.Next(100000, 999999).ToString();

        db.OtpCodes.Add(new OtpCode
        {
            Identifier = identifier.ToLowerInvariant(),
            Code = code,
            Type = type,
            ExpiresAt = DateTime.UtcNow.Add(expiry),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return code;
    }

    public async Task<int> SecondsUntilResendAsync(string identifier, string type, TimeSpan minInterval)
    {
        var lastSent = await db.OtpCodes
            .Where(o => o.Identifier == identifier.ToLowerInvariant() && o.Type == type)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => (DateTime?)o.CreatedAt)
            .FirstOrDefaultAsync();
        if (lastSent is null) return 0;
        var remaining = minInterval - (DateTime.UtcNow - lastSent.Value);
        return remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : 0;
    }

    public async Task<bool> VerifyAsync(string identifier, string type, string code)
    {
        var otp = await db.OtpCodes
            .Where(o => o.Identifier == identifier.ToLowerInvariant()
                     && o.Type == type
                     && !o.IsUsed
                     && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp is null) return false;

        otp.Attempts++;

        if (otp.Code != code)
        {
            if (otp.Attempts >= 5) otp.IsUsed = true;
            await db.SaveChangesAsync();
            return false;
        }

        otp.IsUsed = true;
        await db.SaveChangesAsync();
        return true;
    }
}
