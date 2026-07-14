using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Gigahoo.Api.Services;

/// <summary>
/// Synthesizes a voice sample via DashScope's Qwen3-TTS-Instruct-Flash-Realtime — the ONLY Qwen-TTS
/// model that honours a FREE natural-language <c>instructions</c> voice description (per the docs;
/// the plain HTTP qwen3-tts-instruct-flash ignores it). It's an OpenAI-Realtime-compatible WebSocket,
/// same gateway + Bearer auth as the omni: session.update → input_text_buffer.append/commit →
/// response.audio.delta (base64 PCM) → response.done. Returns ready-to-play WAV bytes.
/// </summary>
public interface IQwenTtsService
{
    Task<(byte[] Audio, string ContentType)> SynthesizeAsync(string text, string voice, string? instruction, CancellationToken ct = default);
}

public class QwenTtsService(IConfiguration config, ILogger<QwenTtsService> logger) : IQwenTtsService
{
    // Same realtime gateway + auth as the omni (env.ts uses this host with a Bearer key); only the
    // model differs. dashscope-intl works with just the API key — no WorkspaceId-scoped URL needed.
    private const string WsUrl = "wss://dashscope-intl.aliyuncs.com/api-ws/v1/realtime?model=qwen3-tts-instruct-flash-realtime";
    private const int SampleRate = 24000; // response_format PCM_24000HZ_MONO_16BIT

    // Serialize any non-ASCII (Chinese instruct, non-English output text) as literal UTF-8.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<(byte[] Audio, string ContentType)> SynthesizeAsync(string text, string voice, string? instruction, CancellationToken ct = default)
    {
        var apiKey = config["DASHSCOPE_API_KEY"] ?? config["Qwen:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("DASHSCOPE_API_KEY is not configured.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeoutCts.Token;

        var sw = Stopwatch.StartNew();
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        await ws.ConnectAsync(new Uri(WsUrl), token);
        var connectMs = sw.ElapsedMilliseconds;

        logger.LogInformation("Qwen-TTS-realtime synth: voice={Voice} instruct=[{Instruct}]", voice, instruction ?? "(none)");

        // 1) Configure the session: voice + optional free-text instruction + PCM output. "commit"
        //    mode means WE trigger synthesis by committing the text buffer.
        var session = new Dictionary<string, object?>
        {
            ["voice"] = voice,
            ["response_format"] = "pcm", // API rejects PCM_24000HZ_MONO_16BIT; supported: mp3/wav/pcm/opus
            ["mode"] = "commit",
        };
        if (!string.IsNullOrWhiteSpace(instruction))
        {
            session["instructions"] = instruction.Length > 500 ? instruction[..500] : instruction;
            // Without this the model accepts `instructions` but doesn't apply it (per the doc example).
            session["optimize_instructions"] = true;
        }
        await SendJsonAsync(ws, new { type = "session.update", session }, token);

        // 2) Feed the text, then commit to start synthesis.
        await SendJsonAsync(ws, new { type = "input_text_buffer.append", text }, token);
        await SendJsonAsync(ws, new { type = "input_text_buffer.commit" }, token);
        var commitMs = sw.ElapsedMilliseconds; // request fully sent

        // 3) Collect base64 PCM from response.audio.delta until response.done.
        using var audio = new MemoryStream();
        var buffer = new byte[32768];
        var failed = false;
        long firstAudioAt = -1;
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var sb = new StringBuilder();
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            while (!result.EndOfMessage)
            {
                result = await ws.ReceiveAsync(buffer, token);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }

            string? evt = null;
            try
            {
                using var doc = JsonDocument.Parse(sb.ToString());
                var root = doc.RootElement;
                evt = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                if (evt == "response.audio.delta" && root.TryGetProperty("delta", out var d))
                {
                    if (firstAudioAt < 0) firstAudioAt = sw.ElapsedMilliseconds;
                    var bytes = Convert.FromBase64String(d.GetString() ?? "");
                    audio.Write(bytes, 0, bytes.Length);
                    continue;
                }

                if (evt == "response.done" || evt == "session.finished")
                    break;

                if (evt == "error" || evt == "response.error")
                {
                    logger.LogError("Qwen-TTS-realtime error event: {Payload}", sb.ToString());
                    failed = true;
                    break;
                }

                // Anything else (session.created/updated, response.created, transcripts, …). Log it
                // once so an unexpected protocol shape surfaces in the logs on the first live test.
                logger.LogInformation("Qwen-TTS-realtime event: {Event}", evt ?? sb.ToString());
            }
            catch (JsonException)
            {
                logger.LogWarning("Qwen-TTS-realtime non-JSON frame: {Payload}", sb.ToString());
            }
        }

        sw.Stop();
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { /* best effort */ }

        if (failed || audio.Length == 0)
        {
            logger.LogError("Qwen-TTS-realtime returned no audio for voice {Voice} (failed={Failed})", voice, failed);
            throw new InvalidOperationException("Voice synthesis failed.");
        }

        var ttfaMs = firstAudioAt < 0 ? -1 : firstAudioAt - commitMs;
        logger.LogInformation("TIMING Qwen-TTS voice={Voice}: connect={Connect}ms ttfa={Ttfa}ms total={Total}ms bytes={Bytes}",
            voice, connectMs, ttfaMs, sw.ElapsedMilliseconds, audio.Length);

        return (WrapPcm16MonoAsWav(audio.ToArray(), SampleRate), "audio/wav");
    }

    private static async Task SendJsonAsync(ClientWebSocket ws, object message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    // Prepend a 44-byte WAV header for signed 16-bit little-endian mono PCM at the given rate.
    private static byte[] WrapPcm16MonoAsWav(byte[] pcm, int sampleRate)
    {
        const int channels = 1, bits = 16;
        int byteRate = sampleRate * channels * bits / 8;
        short blockAlign = (short)(channels * bits / 8);
        using var ms = new MemoryStream(44 + pcm.Length);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + pcm.Length);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1);
        w.Write((short)channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write((short)bits);
        w.Write("data"u8.ToArray());
        w.Write(pcm.Length);
        w.Write(pcm);
        w.Flush();
        return ms.ToArray();
    }
}
