using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{  
  [Table("payments")]
  public class Payment : BaseModel
  {
    [Column("id")]
    public Guid Id { get; set; }
    [Column("order_id")]
    public Guid OrderId { get; set; }
    [Column("stripe_charge_id")]
    public string StripeChargeId { get; set; } = string.Empty;
    [Column("amount_cents")]
    public int AmountCents { get; set; }
    [Column("currency")]
    public string Currency { get; set; } = "CAD";
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
  }
}