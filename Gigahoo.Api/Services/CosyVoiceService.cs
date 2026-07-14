using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Gigahoo.Api.Services;

/// <summary>
/// Synthesizes a short voice sample via Alibaba DashScope's MANAGED CosyVoice
/// (cosyvoice-v3-flash) — no self-hosting. Uses the same DashScope host as the omni,
/// the task-based streaming WebSocket (run-task / continue-task / finish-task).
/// The optional <c>instruction</c> carries the style/emotion (managed instruct control).
/// Returns ready-to-play WAV bytes.
/// </summary>
public interface ICosyVoiceService
{
    Task<byte[]> SynthesizeAsync(string text, string voice, string? instruction, CancellationToken ct = default);
}

public class CosyVoiceService(IConfiguration config, ILogger<CosyVoiceService> logger) : ICosyVoiceService
{
    // Managed DashScope task-based inference WS (Singapore/international), same host as the omni.
    private const string WsUrl = "wss://dashscope-intl.aliyuncs.com/api-ws/v1/inference";
    private const string Model = "cosyvoice-v3-flash";
    private const int SampleRate = 22050; // CosyVoice's documented default; 24000 is rejected (err 428).

    // Serialize the Chinese instruct as LITERAL UTF-8 (not \uXXXX escapes) — the CosyVoice instruct
    // parser rejects the escaped form (err 428). Relaxed encoder passes CJK/punctuation through.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<byte[]> SynthesizeAsync(string text, string voice, string? instruction, CancellationToken ct = default)
    {
        try
        {
            return await SynthesizeOnceAsync(text, voice, instruction, ct);
        }
        catch (InvalidOperationException) when (!string.IsNullOrWhiteSpace(instruction))
        {
            // The style/emotion instruct may be rejected (unsupported format/voice). Retry plain so
            // the voice still previews; the instruct format gets refined separately.
            logger.LogWarning("CosyVoice failed with instruction; retrying without it for voice {Voice}", voice);
            return await SynthesizeOnceAsync(text, voice, null, ct);
        }
    }

    private async Task<byte[]> SynthesizeOnceAsync(string text, string voice, string? instruction, CancellationToken ct)
    {
        logger.LogInformation("CosyVoice synth: voice={Voice} instruct=[{Instruct}]", voice, instruction ?? "(none)");
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

        var taskId = Guid.NewGuid().ToString("N");

        // 1) run-task — open a TTS task with the voice + audio format + (optional) instruct.
        var parameters = new Dictionary<string, object?>
        {
            ["text_type"] = "PlainText",
            ["voice"] = voice,
            ["format"] = "pcm",
            ["sample_rate"] = SampleRate,
            ["volume"] = 50,
            ["rate"] = 1.0,
            ["pitch"] = 1.0,
            ["seed"] = 0,
            ["type"] = 0,
        };
        if (!string.IsNullOrWhiteSpace(instruction))
            parameters["instruction"] = instruction.Length > 128 ? instruction[..128] : instruction;

        var runTask = new
        {
            header = new { action = "run-task", task_id = taskId, streaming = "duplex" },
            payload = new
            {
                model = Model,
                task_group = "audio",
                task = "tts",
                function = "SpeechSynthesizer",
                input = new { },
                parameters,
            },
        };
        logger.LogInformation("CosyVoice run-task JSON: {Json}", JsonSerializer.Serialize(runTask, JsonOpts));
        await SendJsonAsync(ws, runTask, token);

        // The task protocol is ORDERED: we must wait for `task-started` before sending the text.
        // Sending continue-task/finish-task up front races task setup — the text is sometimes
        // dropped and the task never finishes (intermittent 30s hang). So prepare them and send
        // only once task-started arrives.
        var continueMsg = new
        {
            header = new { action = "continue-task", task_id = taskId, streaming = "duplex" },
            payload = new
            {
                model = Model,
                task_group = "audio",
                task = "tts",
                function = "SpeechSynthesizer",
                input = new { text },
            },
        };
        var finishMsg = new
        {
            header = new { action = "finish-task", task_id = taskId, streaming = "duplex" },
            payload = new { input = new { } },
        };

        // Collect binary audio frames until task-finished (text control frames carry the events).
        using var audio = new MemoryStream();
        var buffer = new byte[32768];
        var failed = false;
        var started = false;
        long commitMs = -1, firstAudioAt = -1;
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close) break;

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                if (firstAudioAt < 0) firstAudioAt = sw.ElapsedMilliseconds;
                audio.Write(buffer, 0, result.Count);
                while (!result.EndOfMessage)
                {
                    result = await ws.ReceiveAsync(buffer, token);
                    audio.Write(buffer, 0, result.Count);
                }
                continue;
            }

            // Text control frame — reassemble + inspect the event.
            var sb = new StringBuilder();
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            while (!result.EndOfMessage)
            {
                result = await ws.ReceiveAsync(buffer, token);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            var evt = sb.ToString();
            string? ev = null;
            try
            {
                using var doc = JsonDocument.Parse(evt);
                if (doc.RootElement.TryGetProperty("header", out var h) && h.TryGetProperty("event", out var e))
                    ev = e.GetString();
            }
            catch (JsonException) { /* ignore non-JSON control frame */ }

            if (ev == "task-started" && !started)
            {
                started = true;
                await SendJsonAsync(ws, continueMsg, token);
                await SendJsonAsync(ws, finishMsg, token);
                commitMs = sw.ElapsedMilliseconds; // text request fully sent
            }
            else if (ev == "task-finished")
            {
                break;
            }
            else if (ev == "task-failed")
            {
                logger.LogError("CosyVoice task-failed: {Payload}", evt);
                failed = true;
                break;
            }
        }

        sw.Stop();
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { /* best effort */ }

        if (failed || audio.Length == 0)
        {
            logger.LogError("CosyVoice returned no audio for voice {Voice} (failed={Failed})", voice, failed);
            throw new InvalidOperationException("Voice synthesis failed.");
        }

        var ttfaMs = firstAudioAt < 0 || commitMs < 0 ? -1 : firstAudioAt - commitMs;
        logger.LogInformation("TIMING CosyVoice voice={Voice}: connect={Connect}ms ttfa={Ttfa}ms total={Total}ms bytes={Bytes}",
            voice, connectMs, ttfaMs, sw.ElapsedMilliseconds, audio.Length);

        return WrapPcm16MonoAsWav(audio.ToArray(), SampleRate);
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
