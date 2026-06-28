namespace Gigahoo.Api.Entities;

// A selectable AI-agent voice, owned by an LLM Provider. Making voices data-driven
// (rather than a hardcoded list) means a future LLM-provider swap just seeds its own
// voices and the dashboard + validation pick them up automatically.
public class Voice
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public Provider Provider { get; set; } = null!;
    public string ApiName { get; set; } = null!;  // provider voice id passed to the TTS engine
    public string Label { get; set; } = null!;     // human-friendly label shown in the picker
    public int DisplayOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}
