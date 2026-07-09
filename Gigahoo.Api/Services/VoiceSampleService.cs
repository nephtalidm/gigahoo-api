using System.Text.Json;

namespace Gigahoo.Api.Services;

/// <summary>
/// Synthesizes a short voice sample (the user's greeting in a chosen voice) on demand.
/// Uses <c>qwen3.5-omni-flash</c> — the SAME model family as the live agent — so the preview
/// sounds like the actual call and supports the full multilingual voice set. The model reads
/// the text in ITS OWN language, so a non-English greeting previews with the right accent.
/// Returns ready-to-play WAV bytes.
/// </summary>
public interface IVoiceSampleService
{
    /// <summary>
    /// Synthesize <paramref name="text"/> spoken in <paramref name="voice"/> and return WAV
    /// bytes. Throws on configuration/transport errors.
    /// </summary>
    Task<byte[]> SynthesizeAsync(string text, string voice, string? style = null, CancellationToken ct = default);
}

public class VoiceSampleService(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<VoiceSampleService> logger) : IVoiceSampleService
{
    // OpenAI-compatible chat-completions endpoint. Omni audio output streams back as base64
    // audio deltas (stream=true is required for omni audio).
    private const string GenerationUrl =
        "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";

    public async Task<byte[]> SynthesizeAsync(string text, string voice, string? style = null, CancellationToken ct = default)
    {
        // Map the account's voice-style key to a spoken-tone phrase (mirrors the agent prompt),
        // so the preview reflects the selected personality.
        var tone = style switch
        {
            "warm" => "warm and caring",
            "friendly" => "friendly and approachable",
            "energetic" => "upbeat and energetic",
            "calm" => "calm and reassuring",
            _ => "polished and professional",
        };

        // The key is provided as an env var on the server; accept the appsettings fallback too.
        var apiKey = config["DASHSCOPE_API_KEY"] ?? config["Qwen:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("DASHSCOPE_API_KEY is not configured.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeoutCts.Token;

        var http = httpClientFactory.CreateClient();

        // Read the text aloud VERBATIM in the chosen voice, in the text's own language (so a
        // Spanish greeting previews in Spanish, with a native accent).
        var requestBody = JsonSerializer.Serialize(new
        {
            model = "qwen3.5-omni-flash",
            modalities = new[] { "text", "audio" },
            audio = new { voice, format = "wav" },
            stream = true,
            messages = new object[]
            {
                new { role = "system", content = $"You are a text-to-speech engine. Read the user's message aloud VERBATIM, in the SAME language as the message, in a {tone} tone. Do not add, omit, translate, or change any words, and do not respond conversationally." },
                new { role = "user", content = text },
            },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, GenerationUrl)
        {
            Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(token);
            logger.LogError("Qwen omni TTS error ({Status}): {Payload}", (int)response.StatusCode, err);
            throw new InvalidOperationException("Voice synthesis failed.");
        }

        // Stream the SSE response, concatenating the base64 audio deltas into WAV bytes.
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);
        using var audio = new MemoryStream();
        string? line;
        while ((line = await reader.ReadLineAsync(token)) is not null)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data.Length == 0 || data == "[DONE]") continue;
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("audio", out var au) &&
                    au.TryGetProperty("data", out var dataEl) &&
                    dataEl.GetString() is { Length: > 0 } b64)
                {
                    var bytes = Convert.FromBase64String(b64);
                    audio.Write(bytes, 0, bytes.Length);
                }
            }
            catch (JsonException)
            {
                // Ignore a malformed SSE chunk.
            }
        }

        if (audio.Length == 0)
        {
            logger.LogError("Qwen omni TTS returned no audio for voice {Voice}", voice);
            throw new InvalidOperationException("Voice synthesis failed.");
        }

        // Omni streams RAW PCM (s16le, 24kHz, mono) in the audio deltas — NOT a WAV container,
        // even with format=wav — so wrap it in a WAV header the browser can decode and play.
        return WrapPcm16Mono24kAsWav(audio.ToArray());
    }

    // Prepend a 44-byte WAV header for signed 16-bit little-endian, mono, 24 kHz PCM.
    private static byte[] WrapPcm16Mono24kAsWav(byte[] pcm)
    {
        const int sampleRate = 24000, channels = 1, bits = 16;
        int byteRate = sampleRate * channels * bits / 8;
        short blockAlign = (short)(channels * bits / 8);
        using var ms = new MemoryStream(44 + pcm.Length);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + pcm.Length);      // RIFF chunk size
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);                    // fmt chunk size (PCM)
        w.Write((short)1);              // audio format = PCM
        w.Write((short)channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write((short)bits);
        w.Write("data"u8.ToArray());
        w.Write(pcm.Length);            // data chunk size
        w.Write(pcm);
        w.Flush();
        return ms.ToArray();
    }
}
