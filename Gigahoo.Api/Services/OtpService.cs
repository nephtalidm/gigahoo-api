using Gigahoo.Api.Data;
using Gigahoo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Services;

public interface IOtpService
{
    Task<string> GenerateAndStoreAsync(string identifier, string type, TimeSpan expiry);
    Task<bool> VerifyAsync(string identifier, string type, string code);
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
