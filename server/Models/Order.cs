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

  public class ShippingAddress
{
    [Required]
    [Column("recipient_name")]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    [Column("street_address1")]
    public string StreetAddress1 { get; set; } = string.Empty;

    [Column("street_address2")]
    public string? StreetAddress2 { get; set; } // Optional apartment/suite/unit

    [Required]
    [Column("city")]
    public string City { get; set; } = string.Empty;

    [Required]
    [Column("province")]
    public string Province { get; set; } = string.Empty; // Consider enforcing ISO province codes (e.g., "ON", "QC")

    [Required]
    [Column("postal_code")]
    public string PostalCode { get; set; } = string.Empty; // "A1A 1A1" format

    [Required]
    [Column("country")]
    public string Country { get; set; } = "Canada"; // Default to "Canada", but keep configurable
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
    [Required]
    [System.Text.Json.Serialization.JsonPropertyName("shipping_address")]
    [Column("shipping_address")]
    public ShippingAddress ShippingAddress { get; set; } = new();
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