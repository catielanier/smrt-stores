using Microsoft.AspNetCore.Mvc;
using SmrtStores.Models;
using Supabase;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/products")]
  public class ProductController : ControllerBase
  {
    private readonly Client _supabase;

    public ProductController(Client supabase)
    {
      _supabase = supabase;
    }
    [HttpPost()]
    public async Task<ActionResult<Product>> GetProducts()
    {
      var res = await _supabase.From<Product>().Get();
      var products = res.Models;
      return Ok(new {products});
    }
  }
}