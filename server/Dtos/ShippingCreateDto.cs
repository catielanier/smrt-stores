namespace SmrtStores.Dtos
{
  public class ShippingCreateDto
  {
    public int Weight { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Currency { get; set; } = "CAD";
  }
}