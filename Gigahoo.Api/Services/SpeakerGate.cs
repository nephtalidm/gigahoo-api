using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Gigahoo.Api.Services;

/// <summary>
/// Client for the local speaker-gating sidecar. Tees the caller's mic audio to the
/// sidecar and exposes whether the gate is currently OPEN (forward to the LLM) or
/// CLOSED (a confidently non-caller voice — TV / radio / other people / the agent's
/// own echo — should be dropped).
///
/// FAIL-OPEN by construction: if the sidecar is unreachable, slow, or errors at any
/// point, <see cref="IsOpen"/> stays true and the call behaves exactly as it does
/// today. Feeding audio never blocks the call's audio path (bounded drop-oldest queue).
/// </summary>
public sealed class SpeakerGate : IAsyncDisposable
{
    private static readonly Uri GateUrl = new("ws://127.0.0.1:8770");

    private readonly ClientWebSocket? _ws;
    private readonly Channel<byte[]>? _queue;
    private volatile bool _open = true;

    public bool IsOpen => _open;

    private SpeakerGate(ClientWebSocket? ws, Channel<byte[]>? queue)
    {
        _ws = ws;
        _queue = queue;
    }

    /// <summary>Connect to the sidecar for one call. Returns a fail-open (no-op) gate if unavailable.</summary>
    public static async Task<SpeakerGate> ConnectAsync(string voice, CancellationToken token)
    {
        try
        {
            var ws = new ClientWebSocket();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await ws.ConnectAsync(GateUrl, cts.Token);
            await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { voice }),
                WebSocketMessageType.Text, true, token);

            var queue = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });
            var gate = new SpeakerGate(ws, queue);
            _ = gate.SendLoopAsync(token);
            _ = gate.ReadLoopAsync(token);
            return gate;
        }
        catch
        {
            return new SpeakerGate(null, null); // fail-open: gating disabled, call unaffected
        }
    }

    /// <summary>Tee one mic frame to the sidecar. Non-blocking; drops frames if the sidecar backs up.</summary>
    public void Feed(byte[] audio) => _queue?.Writer.TryWrite(audio);

    private async Task SendLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (var audio in _queue!.Reader.ReadAllAsync(token))
            {
                if (_ws!.State != WebSocketState.Open) break;
                await _ws.SendAsync(audio, WebSocketMessageType.Binary, true, token);
            }
        }
        catch { _open = true; } // sender died -> fail open
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var buf = new byte[1024];
        try
        {
            while (_ws!.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var r = await _ws.ReceiveAsync(buf, token);
                if (r.MessageType == WebSocketMessageType.Close) break;
                if (r.MessageType != WebSocketMessageType.Text) continue;
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, r.Count));
                if (doc.RootElement.TryGetProperty("gate", out var g))
                    _open = g.GetString() != "closed";
            }
        }
        catch { _open = true; } // reader died -> fail open
    }

    public async ValueTask DisposeAsync()
    {
        _queue?.Writer.TryComplete();
        try
        {
            if (_ws is not null)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                _ws.Dispose();
            }
        }
        catch { /* ignore */ }
    }
}
