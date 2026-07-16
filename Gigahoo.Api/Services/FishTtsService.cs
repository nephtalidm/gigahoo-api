using System.Text;
using System.Text.Json;

namespace Gigahoo.Api.Services;

// Fish Audio REST TTS (POST https://api.fish.audio/v1/tts) — renders dashboard voice samples
// with the SAME engine and the SAME voice ids that speak live phone calls, so the preview is
// exactly what callers will hear. Protocol mirrors the voice agent's client (verified there):
// Bearer auth, the model as a HEADER, JSON body with reference_id = the voice id.
public interface IFishTtsService
{
    Task<byte[]> SynthesizeAsync(string text, string referenceId, CancellationToken ct = default);
}

public class FishTtsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<FishTtsService> logger) : IFishTtsService
{
    private const string ApiUrl = "https://api.fish.audio/v1/tts";

    public async Task<byte[]> SynthesizeAsync(string text, string referenceId, CancellationToken ct = default)
    {
        // The key is provided as an env var on the server; accept the appsettings fallback too.
        var apiKey = config["FISH_API_KEY"] ?? config["Fish:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("FISH_API_KEY is not configured.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        var body = JsonSerializer.Serialize(new
        {
            text,
            reference_id = referenceId,
            format = "mp3",
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        request.Headers.TryAddWithoutValidation("model", config["Fish:Model"] ?? "s2.1-pro-free");

        var http = httpClientFactory.CreateClient();
        using var response = await http.SendAsync(request, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            logger.LogError("Fish TTS error ({Status}): {Payload}", (int)response.StatusCode, err);
            throw new InvalidOperationException("Voice synthesis failed.");
        }

        return await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
    }
}
