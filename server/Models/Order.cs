using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("orders")]
class Order : BaseModel
{
  [Column("id")]
  public Guid Id { get; set; }
  [Column("user_id")]
  public Guid UserId { get; set; }
  [Column("total_cents")]
  public int TotalCents { get; set; }
  [Column("currency")]
  public string Currency { get; set; } = "CAD";
  [Column("status")]
  public string Status { get; set; } = "PENDING";
  [Column("stripe_payment_intent_id")]
  public string StripePaymentIntentId { get; set; } = string.Empty;
  [Column("created_at")]
  public DateTime CreatedAt { get; set; }
}