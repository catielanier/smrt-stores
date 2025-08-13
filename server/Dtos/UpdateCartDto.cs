namespace SmrtStores.Dtos
{
  public class UpdateCartDto
  {
    public string ProductNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
  }
}