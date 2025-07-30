using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("users")]
class User : BaseModel
{ 
  [PrimaryKey("id")]
  public Guid Id { get; set; }
  [Column("name")]
  public string Name { get; set; } = string.Empty;
  [Column("email")]
  public string Email { get; set; } = string.Empty;
  [Column("password_hash")]
  public string PasswordHash { get; set; } = string.Empty;
  [Column("phone")]
  public string? Phone { get; set; } = string.Empty;
  [Column("created_at")]
  public DateTime? CreatedAt { get; set; }
  [Column("stripe_customer_id")]
  public string? StripeCustomerId { get; set; } = string.Empty;
}