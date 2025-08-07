using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SmrtStores.Models;

namespace SmrtStores.Dtos
{
  public class OrderCreateDto
  {
    [Required]
    public int TotalCents { get; set; }

    public string Currency { get; set; } = "CAD";

    [Required]
    [JsonPropertyName("shipping_address")]
    public ShippingAddress ShippingAddress { get; set; } = new();

    [Required]
    public ShippingMethod ShippingMethod { get; set; }

    [Required]
    public int ShippingCost { get; set; }
    
    [Required]
    public List<OrderItemCreateDto> Items { get; set; } = new();
  }
}