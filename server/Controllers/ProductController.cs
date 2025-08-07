using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using SmrtStores.Models;
using DBUser = SmrtStores.Models.User;
using Supabase;

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
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
      var res = await _supabase.From<Product>().Get();
      var products = res.Models;
      return Ok(products);
    }

    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct([FromRoute] Guid id)
    {
      var res = await _supabase
        .From<Product>()
        .Where(product => product.Id == id)
        .Get();
      
      if (res.Models is null || res.Models.Count == 0)
        return NotFound();

      return Ok(res.Models.First());
    }

    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPost()]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product) {
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
        .Insert(product);
      if (res.Models is null || res.Models.Count == 0)
        return StatusCode(500, "Failed to create product");
      return CreatedAtAction(
        nameof(GetProduct), 
        new { id = res.Models.First().Id }, 
        res.Models.First()
      );
    }

    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> UpdateProduct([FromRoute] Guid id, [FromBody] Product product)
    {
      if (!ModelState.IsValid)
        return BadRequest(ModelState);

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