namespace Gigahoo.Api.Entities;

public class Language
{
    public byte LanguageId { get; set; }
    public string Name { get; set; } = null!;
    // BCP-47-ish locale code ("en", "es", "yue"). ONE language system: the same rows serve
    // call languages (Conversation/AgentVoice) and dashboard locales (Account). Keep the
    // codes in sync with the UI's shipped dictionaries (lib/i18n).
    public string? Code { get; set; }
}
