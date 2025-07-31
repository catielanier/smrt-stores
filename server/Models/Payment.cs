using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{  
  [Table("payments")]
  public class Payment : BaseModel
  {
    [Column("id")]
    public Guid Id { get; set; }
    [Required]
    [Column("order_id")]
    public Guid OrderId { get; set; }
    [Required]
    [Column("stripe_charge_id")]
    public string StripeChargeId { get; set; } = string.Empty;
    [Required]
    [Column("amount_cents")]
    public int AmountCents { get; set; }
    [Required]
    [Column("currency")]
    public string Currency { get; set; } = "CAD";
    [Required]
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
  }
}