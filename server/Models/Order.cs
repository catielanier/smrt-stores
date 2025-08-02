using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  public enum ShippingMethod
  {
      CanadaPost,
      UPS,
      FedEx,
      Purolator
  }

  public enum ShippingStatus
  {
    Pending,
    Shipped,
    Delivered,
    Returned
  }

  [Table("orders")]
  public class Order : BaseModel
  {
    [Column("id")]
    public Guid Id { get; set; }
    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }
    [Required]
    [Column("total_cents")]
    public int TotalCents { get; set; }
    [Required]
    [Column("currency")]
    public string Currency { get; set; } = "CAD";
    [Required]
    [Column("status")]
    public string Status { get; set; } = "PENDING";
    [Column("shipping_method")]
    public ShippingMethod ShippingMethod { get; set; }
    [Column("shipping_cost")]
    public int ShippingCost { get; set; }
    [Column("shipping_status")]
    public ShippingStatus ShippingStatus { get; set; } = ShippingStatus.Pending;
    [Column("tracking_number")]
    public string? TrackingNumber { get; set; }
    [Column("tracking_url")]
    public string? TrackingUrl{ get; set; }
    [Column("stripe_payment_intent_id")]
    public string StripePaymentIntentId { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
  }
}