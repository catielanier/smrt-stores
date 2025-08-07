using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using SmrtStores.Models;
using Supabase;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/categories")]
  public class CategoryController : ControllerBase
  {
    private readonly Client _supabase;
    private readonly TokenService _tokenService;
    public CategoryController(Client supabase, TokenService tokenService) {
      _supabase = supabase;
      _tokenService = tokenService;
    }

    [HttpGet()]
    public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
    {
      var res = await _supabase.From<Category>().Get();
      var categories = res.Models;
      return Ok(categories);
    }

    [HttpPost()]
    public async Task<ActionResult<Category>> CreateCategory(string categoryName)
    {
      string slug = SlugService.GenerateSlug(categoryName);
      Category category = new Category
      {
        Name = categoryName,
        Slug = slug,
      };

      var res = await _supabase.From<Category>().Insert(category);

      if (res.Models is null || res.Models.Count == 0)
        return StatusCode(500, "Failed to create category");

      return CreatedAtAction(
        nameof(GetCategories), 
        new { id = res.Models.First().Id }, 
        res.Models.First()
      );
    }
  }
}