namespace Gigahoo.Api.Entities;

// Lookup table of conversation outcomes. Seeded with explicit ids
// (must match the ConversationStatusId enum / DB seed rows).
public class ConversationStatus
{
    public byte ConversationStatusId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Conversation> Conversations { get; set; } = [];
}

public enum ConversationStatusId : byte
{
    Missed = 1,
    Answered = 2,
    Completed = 3,
    Live = 4,
}
