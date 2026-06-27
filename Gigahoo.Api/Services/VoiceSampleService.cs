using System.Text.Json;

namespace Gigahoo.Api.Services;

/// <summary>
/// Synthesizes a short voice sample (the user's greeting in a chosen voice) on
/// demand via Qwen's native TTS model (qwen3-tts-flash). Returns ready-to-play
/// WAV bytes.
/// </summary>
public interface IVoiceSampleService
{
    /// <summary>
    /// Synthesize <paramref name="text"/> spoken in <paramref name="voice"/> and
    /// return WAV bytes. Throws on configuration/transport errors.
    /// </summary>
    Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default);
}

public class VoiceSampleService(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<VoiceSampleService> logger) : IVoiceSampleService
{
    private const string GenerationUrl =
        "https://dashscope-intl.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation";

    /// <summary>The voices qwen3-tts-flash may speak with. Keep in sync with the
    /// dashboard voice picker and the AccountController agent-voice allow-list.</summary>
    public static readonly HashSet<string> AllowedVoices =
        new(StringComparer.OrdinalIgnoreCase) { "Serena", "Jennifer", "Katerina", "Kiki", "Sunny", "Ethan", "Ryan", "Aiden", "Marcus", "Peter", "Dylan", "Rocky", "Eric" };

    public async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
    {
        // The key is provided as an env var on the server; accept the appsettings
        // fallback too. Missing key is a server misconfiguration (surfaced as 500).
        var apiKey = config["DASHSCOPE_API_KEY"] ?? config["Qwen:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("DASHSCOPE_API_KEY is not configured.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeoutCts.Token;

        var http = httpClientFactory.CreateClient();

        // qwen3-tts-flash is a dedicated TTS that reads the text literally.
        var requestBody = JsonSerializer.Serialize(new
        {
            model = "qwen3-tts-flash",
            input = new
            {
                text,
                voice
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, GenerationUrl)
        {
            Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        using var response = await http.SendAsync(request, token);
        var payload = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Qwen TTS error ({Status}): {Payload}", (int)response.StatusCode, payload);
            throw new InvalidOperationException("Voice synthesis failed.");
        }

        // The response carries output.audio.url (output.audio.data is empty), a
        // URL to a .wav file we must fetch.
        string? audioUrl;
        using (var doc = JsonDocument.Parse(payload))
        {
            if (!doc.RootElement.TryGetProperty("output", out var output) ||
                !output.TryGetProperty("audio", out var audio) ||
                !audio.TryGetProperty("url", out var urlEl) ||
                urlEl.GetString() is not { Length: > 0 } url)
            {
                logger.LogError("Qwen TTS response missing output.audio.url: {Payload}", payload);
                throw new InvalidOperationException("Voice synthesis failed.");
            }
            audioUrl = url;
        }

        return await http.GetByteArrayAsync(audioUrl, token);
    }
}
