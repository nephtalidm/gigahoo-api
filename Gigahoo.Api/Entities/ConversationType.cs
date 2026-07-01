namespace Gigahoo.Api.Entities;

// Lookup table of conversation channels/types (e.g. Phone Call, Web Call).
// Seeded with explicit ids (must match the ConversationTypeId enum / DB seed rows).
public class ConversationType
{
    public byte ConversationTypeId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Conversation> Conversations { get; set; } = [];
}

public enum ConversationTypeId : byte
{
    PhoneCall = 1,
    WebCall = 2,
}
