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
    [HttpGet()]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
      var res = await _supabase.From<Product>().Get();
      var products = res.Models;
      return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(Guid id)
    {
      var res = await _supabase
        .From<Product>()
        .Where(product => product.Id == id)
        .Get();
      
      if (res.Models is null || res.Models.Count == 0)
        return NotFound();

      return Ok(res.Models.First());
    }

    [HttpPost()]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product) {
      if (!ModelState.IsValid)
        return BadRequest(ModelState);
      var res = await _supabase
        .From<Product>()
        .Insert(product);
      if (res.Models is null || res.Models.Count == 0)
        return StatusCode(500, "Failed to create product");
      return Ok(res.Models.First());
    }
    
    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> UpdateProduct(Guid id, [FromBody] Product product)
    {
      if (!ModelState.IsValid)
        return BadRequest(ModelState);
      var res = await _supabase
        .From<Product>()
        .Where(p => p.Id == id)
        .Update(product);
      if (res.Models is null || res.Models.Count == 0)
        return StatusCode(500, "Failed to update product");
      return Ok(res.Models.First());
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<Product>> DeleteProduct(Guid id)
    {
      var res = await _supabase
        .From<Product>()
        .Where(p => p.Id == id)
        .Get();
      if (res.Models is null || res.Models.Count == 0)
        return NotFound();
      await _supabase
        .From<Product>()
        .Where(p => p.Id == id)
        .Delete();
      return NoContent();
    }
  }
}