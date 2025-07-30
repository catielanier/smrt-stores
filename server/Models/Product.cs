using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("products")]
  public class Product : BaseModel
  {
    [PrimaryKey("id")]
    public Guid Id { get; set; }
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Required]
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Price must be a non-negative value")]
    [Column("price")]
    public int Price { get; set; }
    [Required]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO code")]
    [Column("currency")]
    public string Currency { get; set; } = "CAD";
    [Required]
    [Column("image_url")]
    public string? ImageUrl { get; set; }
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Price must be a non-negative value")]
    [Column("stock")]
    public int Stock { get; set; }
    [Column("is_active")]
    [Required]
    public bool IsActive { get; set; }
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
  }
}