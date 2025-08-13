using Microsoft.AspNetCore.Mvc;
using SmrtStores.Dtos;
using DBUser = SmrtStores.Models.User;
using SmrtStores.Models;
using SmrtStores.Services;
using Supabase;
using System.IdentityModel.Tokens.Jwt;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/cart")]
  public class CartController : ControllerBase
  {
    private readonly Client _supabase;
    private readonly TokenService _tokenService;

    public CartController(Client supabase, TokenService tokenService)
    {
      _supabase = supabase;
      _tokenService = tokenService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CartReturnDto>> GetCart([FromRoute] Guid id)
    {
      // fetch cart
      var cartRes = await _supabase.From<Cart>().Where(c => c.Id == id).Get();
      if (cartRes.Models is null || cartRes.Models.Count == 0)
        return NotFound(new { error = "CART_NOT_FOUND" });

      var cart = cartRes.Models.First();

      // Ownership check ONLY if the cart is user-owned
      if (cart.UserId != null) // nullable Guid? → only enforce auth for user carts
      {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(token))
          return Unauthorized(new { error = "JWT_REQUIRED" });

        var principal = _tokenService.ValidateToken(token);
        if (principal is null)
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

        var userId = _tokenService.GetUserIdFromToken(token);

        var userRes = await _supabase
          .From<DBUser>()
          .Where(o => o.Id == userId)
          .Get();

        if (userRes.Models is null || userRes.Models.Count == 0)
          return Unauthorized(new { error = "USER_LOOKUP_ERROR" });

        // enforce ownership: if cart has a user, it must match the current user
        if (userRes.Models.First().Id != cart.UserId)
          return Unauthorized(new { error = "No access to cart" });
      }
      // else: guest cart → no auth needed

      // hydrate lines
      var linesRes = await _supabase.From<CartLine>().Where(cl => cl.CartId == cart.Id).Get();
      if (linesRes.Models is null)
        return StatusCode(500, new { error = "SUPABASE_ERROR" });

      var lines = linesRes.Models;

      // get distinct product_numbers and fetch their product records
      var productNumbers = lines.Select(l => l.ProductNumber).Distinct().ToList();
      var productsRes = productNumbers.Count == 0
        ? null
        : await _supabase.From<Product>()
            // If your SDK has .In(...) use it; otherwise, use a Filter "in" with a tuple string:
            .Filter("product_number", Supabase.Postgrest.Constants.Operator.In,
                    $"({string.Join(",", productNumbers.Select(n => $"\"{n}\""))})")
            .Get();

      var products = productsRes?.Models ?? new List<Product>();
      var productByNumber = products.ToDictionary(p => p.ProductNumber, p => p);

      // map to DTO
      var dto = new CartReturnDto
      {
        Id = cart.Id,
        Currency = cart.Currency,
        CartItems = lines.Select(line =>
        {
          productByNumber.TryGetValue(line.ProductNumber, out var prod);
          return new CartLineItemDto
          {
            ProductNumber = line.ProductNumber,
            Qty = line.Qty,
            Price = prod?.Price ?? 0,
            Name = prod?.Name ?? string.Empty,
            Slug = prod?.Slug ?? string.Empty,
            ImageUrl = prod?.ImageUrl ?? string.Empty,
          };
        }).ToList()
      };

      return Ok(dto);
    }

    [HttpPost("{id}")]
    public async Task<ActionResult<CartLineItemDto>> UpdateCart([FromRoute] Guid id, [FromBody] UpdateCartDto updateCartDto)
    {
      var cart = await _supabase.From<Cart>().Where(c => c.Id == id).Get();
      if (cart.Models is null || cart.Models.Count == 0)
        return BadRequest("No cart found");
      if (cart.Models.First().UserId != null) // nullable Guid? → only enforce auth for user carts
      {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(token))
          return Unauthorized(new { error = "JWT_REQUIRED" });

        var principal = _tokenService.ValidateToken(token);
        if (principal is null)
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

        var userId = _tokenService.GetUserIdFromToken(token);

        var userRes = await _supabase
          .From<DBUser>()
          .Where(o => o.Id == userId)
          .Get();

        if (userRes.Models is null || userRes.Models.Count == 0)
          return Unauthorized(new { error = "USER_LOOKUP_ERROR" });

        // enforce ownership: if cart has a user, it must match the current user
        if (userRes.Models.First().Id != cart.Models.First().UserId)
          return Unauthorized(new { error = "No access to cart" });
      }
      var res = await _supabase.From<CartLine>().Where(cl => cl.CartId == id && cl.ProductNumber == updateCartDto.ProductNumber).Get();
      if (res.Models is null)
        return BadRequest("Unknown supabase error");
      var cartItem = res.Models.First();
      if (updateCartDto.Quantity <= 0)
      {
        await _supabase.From<CartLine>().Where(cl => cl.Id == cartItem.Id).Delete();
        return Ok("Item deleted");
      }
      await _supabase.From<CartLine>().Where(cl => cl.Id == cartItem.Id).Set(cl => cl.Qty, updateCartDto.Quantity).Update();
      var product = await _supabase.From<Product>().Where(p => p.ProductNumber == updateCartDto.ProductNumber).Single();
      CartLineItemDto dto = new CartLineItemDto
      {
        ProductNumber = updateCartDto.ProductNumber,
        Name = product!.Name,
        ImageUrl = product!.ImageUrl ?? "",
        Slug = product!.Slug,
        Price = product!.Price,
        Qty = updateCartDto.Quantity
      };

      return Ok(dto);
    }
  }
}
