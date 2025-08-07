using System.ComponentModel.DataAnnotations;

namespace SmrtStores.Dtos
{
  public class OrderItemCreateDto
  {
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public int PriceCents { get; set; }
  }
}