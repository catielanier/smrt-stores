using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SmrtStores.Models;

namespace SmrtStores.Dtos
{
  public class OrderUpdateDto
  {
    public string Status { get; set; } = "PENDING";
    public string TrackingNumber { get; set; } = string.Empty;
    public string TrackingUrl { get; set; } = string.Empty;
  }
}