using System.ComponentModel.DataAnnotations;

namespace SmrtStores.Dtos
{
  public class CartLineItemDto
  {
    [Required]
    public string ProductNumber { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Slug { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    [Required]
    public int Qty { get; set; }
    [Required]
    public int Price { get; set; }
  }
  public class CartReturnDto
  {
    public Guid Id { get; set; }
    public string Currency { get; set; } = "CAD";
    [Required]
    public List<CartLineItemDto> CartItems { get; set; } = new List<CartLineItemDto> { };
  }
}