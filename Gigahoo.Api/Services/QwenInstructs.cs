namespace Gigahoo.Api.Services;

/// <summary>
/// Creative, free-English instruct presets for Qwen3-TTS-Instruct (the "Speaking context" the user
/// picks). Unlike CosyVoice, Qwen takes arbitrary natural language, so these can be as theatrical as
/// we like. Voice-independent — the same presets work on every Qwen voice.
/// </summary>
public static class QwenInstructs
{
    // Skewed to the LOW-AROUSAL / prosody-based family — the emotions these models actually render
    // (like 😢 heartbroken). High-arousal emotions (angry/excited) need timbre changes the model
    // can't produce on a preset voice, so they're intentionally omitted.
    private static readonly List<InstructOption> PresetList =
    [
        new("heartbroken", "😢 Heartbroken & sobbing"),
        new("tearful", "🥺 Gentle & tearful"),
        new("sad", "😔 Sad & downcast"),
        new("gentle", "🕊️ Gentle & tender"),
        new("soothing", "🧘 Calm & soothing"),
        new("warm", "😊 Warm & welcoming"),
        new("tender", "💗 Loving & tender"),
        new("solemn", "🕯️ Solemn & serious"),
        new("weary", "😪 Tired & weary"),
        new("whisper", "🤫 Soft whisper"),
    ];

    // Concrete VOCAL-QUALITY descriptions (gender/pitch/speed/emotion/timbre), per the Qwen docs'
    // #1 rule — describe the voice you want, never roleplay or imitation. Our earlier roleplay
    // instructions ("act like an angry Karen") rendered flat; these describe the actual delivery.
    private static readonly Dictionary<string, string> Instructions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["heartbroken"] = "Slow, soft, trembling and tearful voice, breaking and shaky, on the verge of sobbing, low and mournful.",
        ["tearful"] = "Soft, fragile, quivering voice, slow and hushed, gentle and close to tears.",
        ["sad"] = "Slow, low, quiet and downcast voice, heavy and subdued, with a falling, weary intonation.",
        ["gentle"] = "Soft, warm and tender voice, slow and soothing, calm and reassuring.",
        ["soothing"] = "Very soft, slow, low and breathy voice, deeply calm, relaxing and reassuring.",
        ["warm"] = "Warm, friendly and gentle voice, softly smiling and welcoming, at a relaxed, moderate pace.",
        ["tender"] = "Soft, affectionate and caring voice, warm and loving, slow and close.",
        ["solemn"] = "Slow, low, grave and serious voice, measured, hushed and weighty.",
        ["weary"] = "Slow, soft, drained and weary voice, low in energy, with a quiet, sighing quality.",
        ["whisper"] = "Hushed, breathy whisper, very soft and slow, intimate and quiet.",
    };

    private static readonly Dictionary<string, string> EmotionTone = new(StringComparer.OrdinalIgnoreCase)
    {
        ["happy"] = "Speak in a happy, cheerful tone.",
        ["sad"] = "Speak in a sad, downcast tone.",
        ["angry"] = "Speak in an angry, irritated tone.",
        ["fearful"] = "Speak in a fearful, anxious tone.",
        ["surprised"] = "Speak in a surprised, astonished tone.",
        ["disgusted"] = "Speak in a disgusted, repulsed tone.",
    };

    public static IReadOnlyList<InstructOption> Options() => PresetList;

    public static bool IsValid(string? key) => string.IsNullOrEmpty(key) || Instructions.ContainsKey(key);

    /// <summary>
    /// Build the free-English instruction: a creative preset wins; otherwise fall back to the plain
    /// emotion tone; otherwise null (neutral).
    /// </summary>
    public static string? Build(string? contextKey, string? emotion)
    {
        if (!string.IsNullOrWhiteSpace(contextKey) && Instructions.TryGetValue(contextKey, out var preset))
            return preset;
        if (!string.IsNullOrWhiteSpace(emotion) && EmotionTone.TryGetValue(emotion, out var tone))
            return tone;
        return null;
    }
}
