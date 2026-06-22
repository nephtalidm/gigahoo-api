using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Gigahoo.Api.Services;

public interface ISmsService
{
    Task SendVerificationCodeAsync(string phoneNumber, string code);
}

public class SmsService(IConfiguration config, ILogger<SmsService> logger) : ISmsService
{
    public async Task SendVerificationCodeAsync(string phoneNumber, string code)
    {
        try
        {
            TwilioClient.Init(config["Twilio:AccountSid"], config["Twilio:AuthToken"]);

            await MessageResource.CreateAsync(
                body: $"Your Gigahoo verification code is: {code}. It expires in 10 minutes.",
                from: new Twilio.Types.PhoneNumber(config["Twilio:FromNumber"]!),
                to: new Twilio.Types.PhoneNumber(phoneNumber)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS to {Phone}", phoneNumber);
        }
    }
}
