using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("categories")]
  public class Category : BaseModel
  {
    [PrimaryKey("id")]
    public Guid Id { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;
  }
}