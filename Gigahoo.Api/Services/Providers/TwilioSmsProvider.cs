using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Gigahoo.Api.Services.Providers;

/// <summary>
/// Twilio implementation of <see cref="ISmsProvider"/> for general outbound SMS.
/// </summary>
public class TwilioSmsProvider(IConfiguration config, ILogger<TwilioSmsProvider> logger) : ISmsProvider
{
    public async Task SendAsync(string toE164, string body)
    {
        try
        {
            TwilioClient.Init(config["Twilio:AccountSid"], config["Twilio:AuthToken"]);

            await MessageResource.CreateAsync(
                body: body,
                from: new Twilio.Types.PhoneNumber(config["Twilio:FromNumber"]!),
                to: new Twilio.Types.PhoneNumber(toE164)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS to {Phone}", toE164);
        }
    }
}
