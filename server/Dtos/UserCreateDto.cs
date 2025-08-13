using System.ComponentModel.DataAnnotations;

namespace SmrtStores.Dtos
{
  public class UserCreateDto
  {
    [Required, StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(32)]
    public string? Phone { get; set; }

    [Required, StringLength(128, MinimumLength = 8,
      ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string VerifyPassword { get; set; } = string.Empty;
  }
}