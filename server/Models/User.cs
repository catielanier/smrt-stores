using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SmrtStores.Models
{ 
  public enum Role
  {
    User,
    Owner
  }
  [Table("users")]
  public class User : BaseModel
  { 
    [PrimaryKey("id")]
    public Guid Id { get; set; }
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Required]
    [Column("role")]
    public Role Role { get; set; } = Role.User;
    [Required]
    [Column("email")]
    public string Email { get; set; } = string.Empty;
    [Required]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;
    [Column("phone")]
    public string? Phone { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
    [Column("stripe_customer_id")]
    public string? StripeCustomerId { get; set; } = string.Empty;
  }
}