using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using SmrtStores.Models;
using DBUser = SmrtStores.Models.User;
using Supabase;
using SmrtStores.Dtos;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/products")]
  public class ProductController : ControllerBase
  {
    private readonly Client _supabase;
    private readonly TokenService _tokenService;

    public ProductController(Client supabase, TokenService tokenService)
    {
      _supabase = supabase;
      _tokenService = tokenService;
    }

    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet()]
    public async Task<ActionResult<IEnumerable<ProductGetDto>>> GetProducts([FromQuery] string? category)
    {
      if (string.IsNullOrWhiteSpace(category))
      {
        var res = await _supabase.From<Product>().Get();
        List<ProductGetDto> products = res.Models.Select(p => new ProductGetDto
        {
          Name = p.Name,
          Description = p.Description,
          Price = p.Price,
          Currency = p.Currency,
          ImageUrl = p.ImageUrl,
          Slug = p.Slug,
          ProductNumber = p.ProductNumber,
          Stock = p.Stock,
          IsActive = p.IsActive,
          Weight = p.Weight,
        }).ToList();        
        return Ok(products);
      }

      var catRes = await _supabase
        .From<Category>()
        .Where(c => c.Slug == category)
        .Get();

      if (catRes.Models is null || catRes.Models.Count == 0)
        return BadRequest("Invalid category");

      Guid categoryId = catRes.Models.First().Id;

      var prdCatRes = await _supabase
        .From<ProductCategory>()
        .Where(pc => pc.CategoryId == categoryId)
        .Get();

      if (prdCatRes.Models is null || prdCatRes.Models.Count == 0)
        return Ok(new List<Product>());

      var productIds = prdCatRes.Models.Select(pc => pc.ProductId).ToList();

      var filteredProductsRes = await _supabase
        .From<Product>()
        .Filter("id", Supabase.Postgrest.Constants.Operator.In, $"({string.Join(",", productIds.Select(id => $"\"{id}\""))})") 
        .Get();

      List<ProductGetDto> filteredProducts = filteredProductsRes.Models.Select(p => new ProductGetDto
        {
          Name = p.Name,
          Description = p.Description,
          Price = p.Price,
          Currency = p.Currency,
          ImageUrl = p.ImageUrl,
          Slug = p.Slug,
          ProductNumber = p.ProductNumber,
          Stock = p.Stock,
          IsActive = p.IsActive,
          Weight = p.Weight,
        }).ToList();

      return Ok(filteredProducts);
    }

    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductGetDto>> GetProduct([FromRoute] String slug)
    {
      var res = await _supabase
        .From<Product>()
        .Where(product => product.Slug == slug)
        .Get();
      
      if (res.Models is null || res.Models.Count == 0)
        return NotFound();

      ProductGetDto product = new ProductGetDto
      {
        Name = res.Models.First().Name,
        Description = res.Models.First().Description,
        Price = res.Models.First().Price,
        Currency = res.Models.First().Currency,
        ImageUrl = res.Models.First().ImageUrl,
        Slug = res.Models.First().Slug,
        ProductNumber = res.Models.First().ProductNumber,
        Stock = res.Models.First().Stock,
        IsActive = res.Models.First().IsActive,
        Weight = res.Models.First().Weight,
      };

      return Ok(product);
    }

    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPost()]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] ProductCreateDto dto) {
      var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

      if (string.IsNullOrWhiteSpace(token))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var principal = _tokenService.ValidateToken(token);

      if (principal is null)
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var expClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

      if (expClaim is null || !long.TryParse(expClaim.Value, out long expUnix))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var userId = _tokenService.GetUserIdFromToken(token);

      var user = await _supabase
        .From<DBUser>()
        .Where(o => o.Id == userId)
        .Get();

      if (user.Models is null || user.Models.Count == 0)
        return BadRequest("USER_LOOKUP_ERROR");

      if (user.Models.First().Role != Role.Owner)
        return Unauthorized(new { error = "No admin access" });

      int productCount = await _supabase.From<Product>().Count(Supabase.Postgrest.Constants.CountType.Exact);

      Product product = new Product
      {
        Name = dto.Name,
        Description = dto.Description,
        Price = dto.Price,
        Currency = dto.Currency,
        ImageUrl = dto.ImageUrl,
        Stock = dto.Stock,
        IsActive = dto.IsActive,
        Weight = dto.Weight,
        Slug = SlugService.GenerateSlug(dto.Name),
        ProductNumber = $"ITEM-{productCount + 1:D6}",
      };

      var res = await _supabase
        .From<Product>()
        .Insert(product);
      if (res.Models is null || res.Models.Count == 0)
        return StatusCode(500, "Failed to create product");
      var createdProduct = res.Models.FirstOrDefault();
      foreach (var categoryId in dto.CategoryIds)
      {
        await _supabase
          .From<ProductCategory>()
          .Insert(new ProductCategory
          {
            ProductId = createdProduct!.Id,
            CategoryId = categoryId
          });
      }
      return CreatedAtAction(
        nameof(GetProduct), 
        new { productNumber = res.Models.First().ProductNumber }, 
        res.Models.First()
      );
    }

    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> UpdateProduct([FromRoute] Guid id, [FromBody] ProductCreateDto dto)
    {
      var product = new Product
      {
          Id = id,
          Name = dto.Name,
          Description = dto.Description,
          Price = dto.Price,
          Currency = dto.Currency,
          ImageUrl = dto.ImageUrl,
          Stock = dto.Stock,
          IsActive = dto.IsActive,
          Weight = dto.Weight,
      };
      var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

      if (string.IsNullOrWhiteSpace(token))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var principal = _tokenService.ValidateToken(token);

      if (principal is null)
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var expClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

      if (expClaim is null || !long.TryParse(expClaim.Value, out long expUnix))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var userId = _tokenService.GetUserIdFromToken(token);

      var user = await _supabase
        .From<DBUser>()
        .Where(o => o.Id == userId)
        .Get();

      if (user.Models is null || user.Models.Count == 0)
        return BadRequest("USER_LOOKUP_ERROR");

      if (user.Models.First().Role != Role.Owner)
        return Unauthorized(new { error = "No admin access" });
      
      var existing = await _supabase.From<Product>().Where(p => p.Id == id).Get();
      if (existing.Models.Count == 0)
        return NotFound();
      var res = await _supabase
        .From<Product>()
        .Where(p => p.Id == id)
        .Update(product);
      if (res.Models is null || res.Models.Count == 0)
        return StatusCode(500, "Failed to update product");

      await _supabase
        .From<ProductCategory>()
        .Where(pc => pc.ProductId == id)
        .Delete();

      foreach (var catId in dto.CategoryIds)
      {
        await _supabase.From<ProductCategory>().Insert(new ProductCategory
          {
            ProductId = id,
            CategoryId = catId
          });
      }
      return Ok(res.Models.First());
    }

    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<ActionResult<Product>> DeleteProduct([FromRoute] Guid id)
    {
      var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

      if (string.IsNullOrWhiteSpace(token))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var principal = _tokenService.ValidateToken(token);

      if (principal is null)
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var expClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

      if (expClaim is null || !long.TryParse(expClaim.Value, out long expUnix))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var userId = _tokenService.GetUserIdFromToken(token);

      var user = await _supabase
        .From<DBUser>()
        .Where(o => o.Id == userId)
        .Get();

      if (user.Models is null || user.Models.Count == 0)
        return BadRequest("USER_LOOKUP_ERROR");

      if (user.Models.First().Role != Role.Owner)
        return Unauthorized(new { error = "No admin access" });
      
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