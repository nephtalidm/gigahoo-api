namespace Gigahoo.Api.Entities;

// Lookup table of provider categories (LLM, Payment, Phone, SMS, Email). Seeded
// with explicit ids so code/migrations can reference a type without a magic string.
public class ProviderType
{
    public byte ProviderTypeId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Provider> Providers { get; set; } = [];
}

// Stable ProviderType ids (must match the ProviderType seed rows in the DB migration).
public enum ProviderTypeId : byte
{
    Llm = 1,
    Payment = 2,
    Phone = 3,
    Sms = 4,
    Email = 5,
}
