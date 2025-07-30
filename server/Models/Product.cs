using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("products")]
  public class Product : BaseModel
  {
    [PrimaryKey("id")]
    public Guid Id { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    [Column("price")]
    public int Price { get; set; }
    [Column("currency")]
    public string Currency { get; set; } = "CAD";
    [Column("image_url")]
    public string? ImageUrl { get; set; }
    [Column("stock")]
    public int Stock { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; }
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
  }
}