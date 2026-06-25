using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gigahoo.Api.Services.Providers;

/// <summary>
/// Telnyx carrier implementation of <see cref="ITelephonyProvider"/> using the
/// Telnyx Numbers API. https://developers.telnyx.com/api/numbers
/// </summary>
public class TelnyxTelephonyProvider(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<TelnyxTelephonyProvider> logger) : ITelephonyProvider
{
    public string ProviderName => "telnyx";

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.telnyx.com/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config["Telnyx:ApiKey"]);
        return client;
    }

    public async Task<IReadOnlyList<AvailablePhoneNumber>> SearchAvailableAsync(string country, string? areaCode)
    {
        var client = CreateClient();

        var url = $"v2/available_phone_numbers?filter[country_code]={Uri.EscapeDataString(country)}&filter[limit]=10";
        if (!string.IsNullOrWhiteSpace(areaCode))
            url += $"&filter[national_destination_code]={Uri.EscapeDataString(areaCode)}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            logger.LogError("Telnyx number search failed for {Country}: {Status} {Body}", country, response.StatusCode, err);
            return [];
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = new List<AvailablePhoneNumber>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("phone_number", out var pn) && pn.GetString() is { } number)
                    result.Add(new AvailablePhoneNumber(number, country));
            }
        }

        return result;
    }

    public async Task<PurchasedPhoneNumber?> PurchaseAsync(string country, string? areaCode)
    {
        try
        {
            var available = await SearchAvailableAsync(country, areaCode);
            if (available.Count == 0) return null;

            var selected = available[0];
            var client = CreateClient();

            // Order the number. Telnyx returns the order; the resulting number's
            // id (used as our Sid) becomes available once the order completes.
            var payload = new
            {
                phone_numbers = new[] { new { phone_number = selected.PhoneNumber } },
                connection_id = config["Telnyx:ConnectionId"],
                messaging_profile_id = config["Telnyx:MessagingProfileId"]
            };

            var response = await client.PostAsJsonAsync("v2/number_orders", payload);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                logger.LogError("Telnyx number order failed for {Number}: {Status} {Body}", selected.PhoneNumber, response.StatusCode, err);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            // The phone number's own id (the phone_number_id within the order) is the
            // handle used to manage it later. Fall back to the order id if absent.
            string? sid = null;
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("phone_numbers", out var pns) && pns.ValueKind == JsonValueKind.Array)
                {
                    var first = pns.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("id", out var pnId))
                        sid = pnId.GetString();
                }
                if (sid is null && data.TryGetProperty("id", out var orderId))
                    sid = orderId.GetString();
            }

            if (sid is null)
            {
                logger.LogError("Telnyx number order for {Number} returned no id", selected.PhoneNumber);
                return null;
            }

            return new PurchasedPhoneNumber(sid, selected.PhoneNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purchasing Telnyx phone number for {Country}", country);
            return null;
        }
    }

    public async Task ConfigureVoiceWebhookAsync(string sid, string webhookUrl)
    {
        // Telnyx routes inbound voice via a Call Control / Voice Connection rather
        // than a per-number webhook URL. Numbers are bound to a connection at order
        // time (connection_id above). Per-number webhook override is done by updating
        // the connection's webhook_event_url. We update the phone number's connection
        // binding here as a best-effort; full Call Control app wiring is configured
        // on the connection itself.
        // TODO: If a per-number inbound webhook override is required, create/point a
        // dedicated Call Control Application (POST /v2/call_control_applications with
        // webhook_event_url = webhookUrl) and bind this number's connection_id to it.
        try
        {
            var client = CreateClient();
            var payload = new { connection_id = config["Telnyx:ConnectionId"] };
            var response = await client.PatchAsJsonAsync($"v2/phone_numbers/{Uri.EscapeDataString(sid)}", payload);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Telnyx ConfigureVoiceWebhook (connection bind) for {Sid} returned {Status}: {Body}", sid, response.StatusCode, err);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error configuring Telnyx voice routing for {Sid}", sid);
            throw;
        }
    }

    public async Task ReleaseAsync(string sid)
    {
        // Delete the phone number from the account (de-provision at the carrier).
        var client = CreateClient();
        var response = await client.DeleteAsync($"v2/phone_numbers/{Uri.EscapeDataString(sid)}");
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            logger.LogError("Telnyx number release failed for {Sid}: {Status} {Body}", sid, response.StatusCode, err);
            throw new HttpRequestException($"Telnyx release failed: {response.StatusCode}");
        }
    }
}
