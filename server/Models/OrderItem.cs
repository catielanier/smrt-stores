using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("order_items")]
  public class OrderItem : BaseModel
  {
    [Column("id")]
    public Guid Id { get; set; }
    [Column("order_id")]
    public Guid OrderId { get; set; }
    [Column("product_id")]
    public Guid ProductId { get; set; }
    [Column("quantity")]
    public int Quantity { get; set; }
    [Column("price_cents")]
    public int PriceCents { get; set; }
  }
}