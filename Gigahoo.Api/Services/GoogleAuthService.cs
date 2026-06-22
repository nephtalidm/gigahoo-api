using Google.Apis.Auth;

namespace Gigahoo.Api.Services;

public interface IGoogleAuthService
{
    Task<GoogleJsonWebSignature.Payload?> ValidateIdTokenAsync(string idToken);
}

public class GoogleAuthService(IConfiguration config) : IGoogleAuthService
{
    public async Task<GoogleJsonWebSignature.Payload?> ValidateIdTokenAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { config["Google:ClientId"]! }
        };

        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch
        {
            return null;
        }
    }
}
