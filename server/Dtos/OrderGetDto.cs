using SmrtStores.Models;
using System.Text.Json.Serialization;

namespace SmrtStores.Dtos
{
  public class OrderGetDto
  {
    public string OrderNumber { get; set; } = string.Empty;
    public int TotalCents { get; set; }

    public string Currency { get; set; } = "CAD";

    [JsonPropertyName("shipping_address")]
    public ShippingAddress ShippingAddress { get; set; } = new();

    public ShippingMethod ShippingMethod { get; set; }

    public ShippingStatus ShippingStatus { get; set; }

    public string? TrackingNumber { get; set; }

    public string? TrackingUrl { get; set; }

    public int ShippingCost { get; set; }
    
    public List<OrderItem> Items { get; set; } = new();

    public DateTime CreatedAt { get; set; }
  }
}