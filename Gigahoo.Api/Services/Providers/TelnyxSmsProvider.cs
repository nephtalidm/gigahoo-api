using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Gigahoo.Api.Services.Providers;

/// <summary>
/// Telnyx implementation of <see cref="ISmsProvider"/> using the Telnyx Messaging API.
/// https://developers.telnyx.com/api/messaging/send-message
/// </summary>
public class TelnyxSmsProvider(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<TelnyxSmsProvider> logger) : ISmsProvider
{
    public async Task SendAsync(string toE164, string body)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.telnyx.com/");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config["Telnyx:ApiKey"]);

            // Send via a messaging profile so Telnyx selects the from-number.
            var payload = new
            {
                messaging_profile_id = config["Telnyx:MessagingProfileId"],
                to = toE164,
                text = body
            };

            var response = await client.PostAsJsonAsync("v2/messages", payload);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                logger.LogError("Telnyx SMS send failed for {Phone}: {Status} {Body}", toE164, response.StatusCode, content);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telnyx SMS to {Phone}", toE164);
        }
    }
}
