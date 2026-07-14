using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gigahoo.Api.Controllers;

/// <summary>
/// TEMPORARY: latency benchmark for the MODULAR pipeline's reply path — text BRAIN (qwen-flash) →
/// VOICE (Qwen-TTS Vincent) — so we can decide whether replacing the omni with ASR+LLM+TTS is fast
/// enough before rebuilding the call-session. The ASR (Ears) is upstream/streaming and measured
/// separately. Token-gated; remove after use.
/// </summary>
[ApiController]
[Route("api/voice/pipeline-bench")]
[AllowAnonymous]
[EnableRateLimiting("api")]
public class VoicePipelineBenchController(
    IQwenTtsService qwenTts,
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ILogger<VoicePipelineBenchController> logger) : ControllerBase
{
    private const string Token = "bench-8f3a2c";
    private const string ChatUrl = "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // A representative caller turn (Spanish), as the ASR would have produced it.
    private const string SampleTurn = "Hola, mi refrigerador no funciona y está haciendo un ruido raro.";
    private const string BrainSystem =
        "You are Sarah, a warm bilingual receptionist for an appliance-repair business. Reply in the SAME " +
        "language as the caller, in ONE short natural sentence, moving the call forward (collect name, address, " +
        "and the problem). Begin your reply with an emotion tag like [[warm]] or [[concerned]].";

    [HttpGet]
    public async Task<IActionResult> Bench([FromQuery] string token, [FromQuery] string? brain)
    {
        if (token != Token) return Unauthorized();
        var brainModel = string.IsNullOrWhiteSpace(brain) ? "qwen-flash" : brain;
        var apiKey = config["DASHSCOPE_API_KEY"] ?? config["Qwen:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return StatusCode(500, new { error = "no key" });

        try
        {
            // STAGE 1 — BRAIN: qwen-flash reads the transcript + generates the reply text.
            var http = httpFactory.CreateClient();
            var body = JsonSerializer.Serialize(new
            {
                model = brainModel,
                messages = new[]
                {
                    new { role = "system", content = BrainSystem },
                    new { role = "user", content = SampleTurn },
                },
                max_tokens = 100,
                temperature = 0.5,
            }, JsonOpts);

            var brainSw = Stopwatch.StartNew();
            using var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            using var resp = await http.SendAsync(req, HttpContext.RequestAborted);
            var json = await resp.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            brainSw.Stop();
            var brainMs = brainSw.ElapsedMilliseconds;
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("pipeline-bench brain error ({Status}): {Payload}", (int)resp.StatusCode, json);
                return StatusCode(500, new { error = "brain failed", detail = json });
            }
            string reply;
            using (var doc = JsonDocument.Parse(json))
                reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

            // Strip a leading [[emotion]] tag for the TTS text (mirrors the real pipeline).
            var clean = System.Text.RegularExpressions.Regex.Replace(reply, @"^\s*\[\[\s*\w+\s*\]\]\s*", "");

            // STAGE 2 — VOICE: Qwen-TTS (Vincent) speaks the reply. Wall clock = time to full audio;
            // the internal "TIMING Qwen-TTS … ttfa=" log gives the time-to-first-audio (what matters live).
            var ttsSw = Stopwatch.StartNew();
            var (audio, _) = await qwenTts.SynthesizeAsync(clean, "Vincent", null, HttpContext.RequestAborted);
            ttsSw.Stop();

            return Ok(new
            {
                brainModel,
                transcript = SampleTurn,
                reply,
                brainMs,
                ttsFullMs = ttsSw.ElapsedMilliseconds,
                ttsAudioBytes = audio.Length,
                note = "ttfa (first-audio) is in the 'TIMING Qwen-TTS' log; add ~150ms ASR-finalize for full per-turn latency",
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "pipeline-bench failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
