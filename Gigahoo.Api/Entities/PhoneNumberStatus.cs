namespace Gigahoo.Api.Entities;

// Lookup table of phone-number lifecycle states. Seeded with explicit ids
// (must match the PhoneNumberStatusId enum / DB seed rows).
public class PhoneNumberStatus
{
    public byte PhoneNumberStatusId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<PhoneNumber> PhoneNumbers { get; set; } = [];
}

public enum PhoneNumberStatusId : byte
{
    Available = 1,
    Assigned = 2,
    Released = 3,
}
