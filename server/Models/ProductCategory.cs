using System.ComponentModel.DataAnnotations.Schema;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("product_categories")]
  public class ProductCategory : BaseModel
  {
    [Column("product_id")]
    public Guid ProductId { get; set; }
    [Column("category_id")]
    public Guid CategoryId { get; set; }
  }
}