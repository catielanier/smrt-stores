using SmrtStores.Models;

namespace SmrtStores.Dtos
{
  public class ShippingReturnDto
  {
    public ShippingMethod ShippingMethod { get; set; }
    public string ShippingType { get; set; } = string.Empty;
    public int ShippingCost { get; set; }
    public string Currency { get; set; } = "CAD";
    public int ApproxShippingDaysMin { get; set;}
    public int ApproxShippingDaysMax { get; set; }
  }
}