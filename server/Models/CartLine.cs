using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("cart_lines")]
  public class CartLine : BaseModel
  {
    [PrimaryKey("id")]
    public Guid Id { get; set; }
    [Required]
    [Column("cart_id")]
    public Guid CartId { get; set; }
    [Required]
    [Column("product_number")]
    public string ProductNumber { get; set; } = string.Empty;
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    [Column("qty")]
    public int Qty { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
  }
}