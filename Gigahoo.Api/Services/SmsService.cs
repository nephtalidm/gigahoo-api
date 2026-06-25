using Gigahoo.Api.Services.Providers;

namespace Gigahoo.Api.Services;

public interface ISmsService
{
    Task SendVerificationCodeAsync(string phoneNumber, string code);
}

/// <summary>
/// OTP delivery. The actual carrier send is routed through the configured
/// <see cref="ISmsProvider"/> so OTP and general SMS share one provider.
/// </summary>
public class SmsService(ISmsProvider smsProvider) : ISmsService
{
    public Task SendVerificationCodeAsync(string phoneNumber, string code)
        => smsProvider.SendAsync(
            phoneNumber,
            $"Your Gigahoo verification code is: {code}. It expires in 10 minutes.");
}
