using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Gigahoo.Api.Services;

/// <summary>
/// Synthesizes a short voice sample (the user's greeting in a chosen voice) on
/// demand via the Qwen realtime TTS WebSocket. Returns ready-to-play WAV bytes.
/// </summary>
public interface IVoiceSampleService
{
    /// <summary>
    /// Synthesize <paramref name="text"/> spoken in <paramref name="voice"/> and
    /// return WAV (PCM16, 24kHz, mono) bytes. Throws on configuration/transport errors.
    /// </summary>
    Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default);
}

public class VoiceSampleService(IConfiguration config, ILogger<VoiceSampleService> logger) : IVoiceSampleService
{
    private const string WsUrl = "wss://dashscope-intl.aliyuncs.com/api-ws/v1/realtime?model=qwen3.5-omni-flash-realtime";
    private const int SampleRate = 24000;

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

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        await ws.ConnectAsync(new Uri(WsUrl), token);

        // Configure the session as a verbatim TTS engine in the requested voice.
        var sessionUpdate = JsonSerializer.Serialize(new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                voice,
                output_audio_format = "pcm",
                instructions = "You are a text-to-speech engine. Read the user's message aloud exactly as written, verbatim, in the same language as the message. Do not add, omit, translate, or change any words and do not respond conversationally."
            }
        });
        await SendAsync(ws, sessionUpdate, token);

        var pcm = new List<byte>();
        var buffer = new byte[16 * 1024];
        var messageBytes = new List<byte>();
        var requested = false;

        while (ws.State == WebSocketState.Open)
        {
            messageBytes.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                    return WrapWav(pcm.ToArray());
                }
                messageBytes.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(messageBytes.ToArray());
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;

            switch (type)
            {
                case "session.updated":
                    // Session is ready: submit the text, then ask for the response.
                    if (!requested)
                    {
                        requested = true;
                        var itemCreate = JsonSerializer.Serialize(new
                        {
                            type = "conversation.item.create",
                            item = new
                            {
                                type = "message",
                                role = "user",
                                content = new[] { new { type = "input_text", text } }
                            }
                        });
                        await SendAsync(ws, itemCreate, token);

                        var responseCreate = JsonSerializer.Serialize(new
                        {
                            type = "response.create",
                            response = new { modalities = new[] { "text", "audio" } }
                        });
                        await SendAsync(ws, responseCreate, token);
                    }
                    break;

                case "response.audio.delta":
                    if (doc.RootElement.TryGetProperty("delta", out var delta) && delta.GetString() is { } b64)
                        pcm.AddRange(Convert.FromBase64String(b64));
                    break;

                case "response.done":
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
                    return WrapWav(pcm.ToArray());

                case "error":
                    logger.LogError("Qwen realtime TTS error: {Payload}", json);
                    throw new InvalidOperationException("Voice synthesis failed.");
            }
        }

        return WrapWav(pcm.ToArray());
    }

    private static Task SendAsync(ClientWebSocket ws, string json, CancellationToken ct) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);

    /// <summary>Wrap raw PCM16 mono bytes in a standard 44-byte WAV header.</summary>
    private static byte[] WrapWav(byte[] pcm)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = SampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + pcm.Length);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);                 // PCM fmt chunk size
        w.Write((short)1);           // PCM format
        w.Write(channels);
        w.Write(SampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(bitsPerSample);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(pcm.Length);
        w.Write(pcm);
        w.Flush();
        return ms.ToArray();
    }
}
