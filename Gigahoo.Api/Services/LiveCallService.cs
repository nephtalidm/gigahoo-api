using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Gigahoo.Api.Services;

/// <summary>
/// Bridges a browser WebSocket to Qwen's omni-realtime voice model so the browser
/// never holds the secret DashScope API key. The API proxies audio both ways:
/// browser mic (PCM16LE mono 16kHz) up to Qwen, and Qwen speech (PCM16LE mono
/// 24kHz) back down to the browser, plus JSON transcript/status events.
/// </summary>
public static class LiveCallService
{
    private const string QwenRealtimeUrl =
        "wss://dashscope-intl.aliyuncs.com/api-ws/v1/realtime?model=qwen3.5-omni-flash-realtime";

    // Maps a locale code to the language name used in the persona directive.
    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["es"] = "Spanish",
        ["fr"] = "French",
        ["zh"] = "Mandarin Chinese",
        ["yue"] = "Cantonese",
        ["hi"] = "Hindi",
        ["pa"] = "Punjabi",
        ["tl"] = "Filipino (Tagalog)",
        ["ko"] = "Korean",
        ["ja"] = "Japanese",
        ["ru"] = "Russian",
        ["uk"] = "Ukrainian",
        ["ar"] = "Arabic",
        ["fa"] = "Persian",
    };

    public static async Task RunAsync(
        WebSocket browser,
        string category,
        string voice,
        string lang,
        IConfiguration config,
        CancellationToken ct)
    {
        var apiKey = config["DASHSCOPE_API_KEY"] ?? config["Qwen:ApiKey"];

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = linkedCts.Token;

        using var qwen = new ClientWebSocket();
        qwen.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        // Send-locks: WebSocket.SendAsync is not safe for concurrent callers, and
        // both pump loops may write to the same socket.
        var browserSendLock = new SemaphoreSlim(1, 1);
        var qwenSendLock = new SemaphoreSlim(1, 1);

        try
        {
            await qwen.ConnectAsync(new Uri(QwenRealtimeUrl), token);
        }
        catch (Exception ex)
        {
            await SendBrowserTextAsync(browser, browserSendLock,
                new { type = "error", message = "Failed to connect to voice service: " + ex.Message }, token);
            await SafeCloseAsync(browser, token);
            return;
        }

        // One-time session.update describing the persona and audio formats.
        var businessKind = string.IsNullOrWhiteSpace(category) ? "home service" : category;
        var languageName = LanguageNames.TryGetValue(lang ?? "en", out var ln) ? ln : "English";
        var persona =
            $"The caller most likely speaks {languageName}, so greet them and begin in {languageName}. " +
            $"But if the caller clearly speaks a different language, immediately switch to that language and " +
            "continue the entire call in it — always mirror the caller's language. " +
            $"You are Sarah, a warm, efficient phone receptionist for a {businessKind} business. " +
            "Keep EVERY reply to ONE short, natural spoken sentence — never give long explanations " +
            "or lists, and never repeat yourself. Let the caller speak and don't fill silences. " +
            "Naturally collect the caller's name, address, and the reason for their call. When the " +
            "caller says goodbye or clearly has nothing more, give a brief one-line farewell and then " +
            "call the end_call function.";

        // Candidate languages for transcription: start with the page locale so the
        // recognizer follows the caller's actual speech instead of defaulting to Chinese.
        var hints = new[] { lang ?? "en", "en", "es", "fr", "zh", "ja", "ko", "ru", "ar", "hi", "de", "it", "pt" }
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct()
            .ToArray();

        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                voice,
                instructions = persona,
                input_audio_format = "pcm16",
                output_audio_format = "pcm16",
                turn_detection = new { type = "server_vad", threshold = 0.5, prefix_padding_ms = 300, silence_duration_ms = 700 },
                input_audio_transcription = new { model = "gummy-realtime-v1", language_hints = hints },
                tools = new object[]
                {
                    new
                    {
                        type = "function",
                        name = "end_call",
                        description = "End the phone call after the caller says goodbye or the conversation is clearly complete.",
                        parameters = new { type = "object", properties = new { } }
                    }
                },
                tool_choice = "auto"
            }
        };

        try
        {
            await SendQwenJsonAsync(qwen, qwenSendLock, sessionUpdate, token);
        }
        catch (Exception ex)
        {
            // If session.update is rejected (e.g. input_audio_transcription not
            // supported), forward an error event but let the call continue.
            await SendBrowserTextAsync(browser, browserSendLock,
                new { type = "error", message = "session.update failed: " + ex.Message }, token);
        }

        var browserToQwen = PumpBrowserToQwenAsync(browser, qwen, qwenSendLock, linkedCts, token);
        var qwenToBrowser = PumpQwenToBrowserAsync(browser, qwen, browserSendLock, linkedCts, token);

        try
        {
            await Task.WhenAll(browserToQwen, qwenToBrowser);
        }
        catch
        {
            // Pump loops swallow their own errors below; this guards the join.
        }
        finally
        {
            linkedCts.Cancel();
            await SafeCloseAsync(qwen, CancellationToken.None);
            await SafeCloseAsync(browser, CancellationToken.None);
        }
    }

    // browser -> Qwen: binary mic frames become input_audio_buffer.append; a
    // {"type":"hangup"} text frame closes the call gracefully.
    private static async Task PumpBrowserToQwenAsync(
        WebSocket browser,
        ClientWebSocket qwen,
        SemaphoreSlim qwenSendLock,
        CancellationTokenSource linkedCts,
        CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        try
        {
            while (!token.IsCancellationRequested && browser.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await browser.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var audioBytes = ms.ToArray();
                    if (audioBytes.Length == 0) continue;
                    var append = new
                    {
                        type = "input_audio_buffer.append",
                        audio = Convert.ToBase64String(audioBytes)
                    };
                    await SendQwenJsonAsync(qwen, qwenSendLock, append, token);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        if (doc.RootElement.TryGetProperty("type", out var t) &&
                            t.GetString() == "hangup")
                        {
                            return;
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore malformed control frames.
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (Exception) { }
        finally
        {
            linkedCts.Cancel();
        }
    }

    // Qwen -> browser: translate realtime events into the browser contract.
    private static async Task PumpQwenToBrowserAsync(
        WebSocket browser,
        ClientWebSocket qwen,
        SemaphoreSlim browserSendLock,
        CancellationTokenSource linkedCts,
        CancellationToken token)
    {
        var buffer = new byte[32 * 1024];
        using var ms = new MemoryStream();
        try
        {
            while (!token.IsCancellationRequested && qwen.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await qwen.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var json = Encoding.UTF8.GetString(ms.ToArray());
                JsonDocument doc;
                try { doc = JsonDocument.Parse(json); }
                catch (JsonException) { continue; }

                using (doc)
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("type", out var typeEl)) continue;
                    var type = typeEl.GetString();

                    switch (type)
                    {
                        case "response.audio.delta":
                            // base64 PCM16 24kHz -> raw bytes as a binary frame.
                            if (root.TryGetProperty("delta", out var audioDelta) &&
                                audioDelta.GetString() is { Length: > 0 } b64)
                            {
                                byte[] pcm;
                                try { pcm = Convert.FromBase64String(b64); }
                                catch (FormatException) { break; }
                                await SendBrowserBinaryAsync(browser, browserSendLock, pcm, token);
                            }
                            break;

                        case "response.audio_transcript.delta":
                            if (root.TryGetProperty("delta", out var agentDelta))
                            {
                                await SendBrowserTextAsync(browser, browserSendLock,
                                    new { type = "agent", text = agentDelta.GetString() ?? "" }, token);
                            }
                            break;

                        case "conversation.item.input_audio_transcription.completed":
                            if (root.TryGetProperty("transcript", out var transcript))
                            {
                                await SendBrowserTextAsync(browser, browserSendLock,
                                    new { type = "user", text = transcript.GetString() ?? "" }, token);
                            }
                            break;

                        case "input_audio_buffer.speech_started":
                            await SendBrowserTextAsync(browser, browserSendLock,
                                new { type = "speech_started" }, token);
                            break;

                        case "response.done":
                            await SendBrowserTextAsync(browser, browserSendLock,
                                new { type = "agent_done" }, token);
                            break;

                        case "response.function_call_arguments.done":
                            // Agent finished invoking a tool. End the call if it's end_call.
                            if (root.TryGetProperty("name", out var fnName) &&
                                fnName.ValueKind == JsonValueKind.String &&
                                fnName.GetString() == "end_call")
                            {
                                await EndCallAsync(browser, qwen, browserSendLock, token);
                                return;
                            }
                            break;

                        case "response.output_item.done":
                            // Some realtime variants report the call as an output item.
                            if (root.TryGetProperty("item", out var item) &&
                                item.ValueKind == JsonValueKind.Object &&
                                item.TryGetProperty("type", out var itemType) &&
                                itemType.ValueKind == JsonValueKind.String &&
                                itemType.GetString() == "function_call" &&
                                item.TryGetProperty("name", out var itemName) &&
                                itemName.ValueKind == JsonValueKind.String &&
                                itemName.GetString() == "end_call")
                            {
                                await EndCallAsync(browser, qwen, browserSendLock, token);
                                return;
                            }
                            break;

                        case "error":
                            string message = "Voice service error.";
                            if (root.TryGetProperty("error", out var errObj))
                            {
                                if (errObj.ValueKind == JsonValueKind.Object &&
                                    errObj.TryGetProperty("message", out var em))
                                    message = em.GetString() ?? message;
                                else if (errObj.ValueKind == JsonValueKind.String)
                                    message = errObj.GetString() ?? message;
                            }
                            else if (root.TryGetProperty("message", out var topMsg))
                            {
                                message = topMsg.GetString() ?? message;
                            }
                            await SendBrowserTextAsync(browser, browserSendLock,
                                new { type = "error", message }, token);
                            return;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (Exception) { }
        finally
        {
            linkedCts.Cancel();
        }
    }

    private static async Task SendQwenJsonAsync(
        ClientWebSocket qwen, SemaphoreSlim sendLock, object payload, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await sendLock.WaitAsync(token);
        try
        {
            await qwen.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, token);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static async Task SendBrowserTextAsync(
        WebSocket browser, SemaphoreSlim sendLock, object payload, CancellationToken token)
    {
        if (browser.State != WebSocketState.Open) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await sendLock.WaitAsync(token);
        try
        {
            if (browser.State == WebSocketState.Open)
                await browser.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true, token);
        }
        catch (Exception) { }
        finally
        {
            sendLock.Release();
        }
    }

    private static async Task SendBrowserBinaryAsync(
        WebSocket browser, SemaphoreSlim sendLock, byte[] bytes, CancellationToken token)
    {
        if (browser.State != WebSocketState.Open) return;
        await sendLock.WaitAsync(token);
        try
        {
            if (browser.State == WebSocketState.Open)
                await browser.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Binary, true, token);
        }
        catch (Exception) { }
        finally
        {
            sendLock.Release();
        }
    }

    // Notify the browser that the agent ended the call, then gracefully close both
    // sockets so the outer Task.WhenAll completes and the call tears down.
    private static async Task EndCallAsync(
        WebSocket browser, ClientWebSocket qwen, SemaphoreSlim browserSendLock, CancellationToken token)
    {
        await SendBrowserTextAsync(browser, browserSendLock, new { type = "call_ended" }, token);
        await SafeCloseAsync(browser, CancellationToken.None);
        await SafeCloseAsync(qwen, CancellationToken.None);
    }

    private static async Task SafeCloseAsync(WebSocket socket, CancellationToken token)
    {
        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
            }
        }
        catch (Exception) { }
    }
}
