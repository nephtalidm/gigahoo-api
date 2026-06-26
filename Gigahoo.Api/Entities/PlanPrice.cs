namespace Gigahoo.Api.Entities;

// One row per (plan, currency). Lets prices/currencies be added or adjusted with a
// simple insert/update — no schema change. StripePriceId is the recurring Stripe price
// used at checkout for that currency; Amount is the display amount in that currency.
public class PlanPrice
{
    public int Id { get; set; }
    public byte PlanId { get; set; }
    public string Currency { get; set; } = null!;   // ISO 4217, e.g. "USD", "MXN", "CAD"
    public string? StripePriceId { get; set; }       // recurring price id from Stripe (set later)
    public decimal Amount { get; set; }              // display amount in that currency
    public bool IsActive { get; set; } = true;
    public DateTime? ReplacedOn { get; set; }         // when this price was last replaced (NULL = never; auto-stamped by a DB trigger)
    public Plan Plan { get; set; } = null!;
}
