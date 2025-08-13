using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{
  [Table("carts")]
  public class Cart : BaseModel
  {
    [PrimaryKey("id")]
    public Guid Id { get; set; }
    [Column("user_id")]
    public Guid UserId { get; set; }
    [Column("currency")]
    public string Currency { get; set; } = "CAD";
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
  }
}