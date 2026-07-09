namespace Gigahoo.Api.Services;

// One selectable "speaking context" for an instruct-capable CosyVoice voice — a scenario, role,
// or identity. `Key` is what we store/receive ("role:温和客服"); `Label` is the English UI text.
public record InstructOption(string Key, string Label);

/// <summary>
/// CosyVoice-v3-flash instruct catalog. Users choose an emotion + optional speaking context in
/// ENGLISH; we assemble the required Chinese instruct template here (CosyVoice mandates Chinese).
/// Per-voice option lists come straight from the DashScope voice list — only the 3 instruct-capable
/// voices available in Singapore are represented. Emotion is universal; context is voice-specific.
/// </summary>
public static class InstructCatalog
{
    public static readonly HashSet<string> Emotions = new(StringComparer.OrdinalIgnoreCase)
        { "neutral", "happy", "sad", "angry", "fearful", "surprised", "disgusted" };

    // Per-voice context options. Key = "<type>:<chinese>", type ∈ scenario|role|identity.
    private static readonly Dictionary<string, List<InstructOption>> Options = new()
    {
        ["longanyang"] = new()
        {
            new("role:一个暴怒到极点的Karen", "😡 Super mega angry Karen"),
            new("role:激情四射、声嘶力竭的拍卖师", "🔥 Screaming hype auctioneer"),
            new("role:阴森恐怖的鬼故事讲述者", "👻 Creepy horror narrator"),
            new("role:痛哭流涕、悲痛欲绝的人", "😭 Sobbing drama queen"),
            new("role:趾高气扬的大反派", "😈 Over-the-top villain"),
            new("scenario:闲聊互动", "Casual conversation"),
            new("scenario:新闻播报", "News broadcast"),
            new("scenario:广告促销", "Ad promotion"),
            new("scenario:比赛解说", "Sports commentary"),
            new("scenario:一些儿童内容解说", "Children's content"),
            new("scenario:语音导航", "Voice navigation"),
            new("scenario:脱口秀表演", "Stand-up comedy"),
            new("role:一个旁白", "Narrator"),
            new("identity:故事机", "Storytelling machine"),
        },
        ["longanhuan"] = new()
        {
            new("role:温和客服", "Gentle customer service"),
            new("scenario:闲聊对话", "Casual conversation"),
            new("scenario:比赛解说", "Sports commentary"),
            new("scenario:深夜电台广播", "Late-night radio"),
            new("scenario:诗歌朗诵", "Poetry reading"),
            new("scenario:科普知识推广", "Science popularization"),
            new("scenario:产品推广", "Product promotion"),
            new("scenario:脱口秀表演", "Stand-up comedy"),
        },
        ["longhuhu_v3"] = new()
        {
            new("role:一个暴怒到极点的Karen", "😡 Super mega angry Karen"),
            new("role:超级兴奋、蹦蹦跳跳的小朋友", "🤩 Hyper excited kid"),
            new("role:阴森恐怖的小幽灵", "👻 Spooky little ghost"),
            new("role:委屈巴巴、放声大哭的小孩", "😭 Wailing tantrum kid"),
            new("scenario:自由对话", "Free conversation"),
            new("scenario:广告促销", "Ad promotion"),
            new("role:温和客服", "Gentle customer service"),
            new("role:傲娇公主", "Tsundere princess"),
            new("role:元气少女", "Energetic girl"),
            new("role:可爱孩童", "Cute child"),
            new("role:机器人", "Robot"),
            new("identity:故事机", "Storytelling machine"),
            new("identity:儿童玩具", "Children's toy"),
        },
    };

    public static IReadOnlyList<InstructOption> OptionsFor(string voice) =>
        Options.TryGetValue(voice, out var opts) ? opts : [];

    public static bool IsInstructVoice(string voice) => Options.ContainsKey(voice);

    public static bool IsValidContext(string voice, string? contextKey) =>
        string.IsNullOrEmpty(contextKey) ||
        (Options.TryGetValue(voice, out var opts) && opts.Any(o => o.Key == contextKey));

    /// <summary>
    /// Assemble the Chinese instruct from an English emotion + optional context key. Returns null
    /// for a non-instruct voice (so plain synthesis is used).
    /// </summary>
    public static string? Build(string voice, string? emotion, string? contextKey)
    {
        if (!IsInstructVoice(voice)) return null;
        var e = emotion is not null && Emotions.Contains(emotion) ? emotion.ToLowerInvariant() : "neutral";

        if (!string.IsNullOrEmpty(contextKey) && IsValidContext(voice, contextKey))
        {
            var idx = contextKey.IndexOf(':');
            if (idx > 0)
            {
                var type = contextKey[..idx];
                var val = contextKey[(idx + 1)..];
                return type switch
                {
                    "scenario" => $"你正在进行{val}，你说话的情感是{e}。",
                    "role" => $"你现在说话的角色是{val}，你说话的情感是{e}。",
                    "identity" => $"你正在以一个{val}的身份说话，你说话的情感是{e}。",
                    _ => $"你说话的情感是{e}。",
                };
            }
        }
        return $"你说话的情感是{e}。";
    }
}
