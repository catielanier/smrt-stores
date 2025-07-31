using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("order_items")]
  public class OrderItem : BaseModel
  {
    [Column("id")]
    public Guid Id { get; set; }
    [Required]
    [Column("order_id")]
    public Guid OrderId { get; set; }
    [Required]
    [Column("product_id")]
    public Guid ProductId { get; set; }
    [Required]
    [Column("quantity")]
    public int Quantity { get; set; }
    [Required]
    [Column("price_cents")]
    public int PriceCents { get; set; }
  }
}