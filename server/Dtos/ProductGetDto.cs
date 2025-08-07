namespace SmrtStores.Dtos
{
  public class ProductGetDto
  {
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int Price { get; set; }
    public string Currency { get; set; } = "CAD";
    public string? ImageUrl { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }
  }
}