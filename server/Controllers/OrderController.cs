using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using SmrtStores.Models;
using DBUser = SmrtStores.Models.User;
using Supabase;
using Microsoft.Net.Http.Headers;
using Stripe;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/orders")]
  public class OrderController : ControllerBase
  {
    private readonly Client _supabase;
    private readonly TokenService _tokenService;
    private readonly StripeClient _stripeClient;
    public OrderController(Client supabase, TokenService tokenService, StripeClient stripeClient)
    {
      _supabase = supabase;
      _tokenService = tokenService;
      _stripeClient = stripeClient;
    }

    [HttpGet()]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
      var res = await _supabase.From<Order>().Get();
      var orders = res.Models;
      return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder([FromRoute] Guid id)
    {
      var res = await _supabase
        .From<Order>()
        .Where(o => o.Id == id)
        .Get();

      if (res.Models is null || res.Models.Count == 0)
        return NotFound();

      return Ok(res.Models.First());
    }

    [HttpGet("by-user/{id}")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByUser([FromRoute] Guid id)
    {
      var checkUser = await _supabase
        .From<DBUser>()
        .Where(u => u.Id == id)
        .Get();
      if (checkUser.Models is null || checkUser.Models.Count == 0)
        return BadRequest("No such user exists");
      var res = await _supabase
        .From<Order>()
        .Where(o => o.UserId == id)
        .Get();
      return Ok(res.Models);
    }

    [HttpGet("by-user/logged-in")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersBySignedInUser()
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

      var res = await _supabase
        .From<Order>()
        .Where(o => o.UserId == userId)
        .Get();

      return Ok(res.Models);
    }

    [HttpPost()]
    public async Task<ActionResult<Order>> CreateOrder(Order order)
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

      if (userId == null)
        return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      order.UserId = (Guid)userId;

      var user = await _supabase
        .From<DBUser>()
        .Where(u => u.Id == userId)
        .Get();

      var stripeOptions = new PaymentIntentCreateOptions
      {
        Customer = user.Models.First().StripeCustomerId,
        Amount = order.TotalCents + order.ShippingCost,
        Currency = order.Currency,
        AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
        {
          Enabled = false,
        }
      };

      var paymentIntentService = new PaymentIntentService();

      PaymentIntent paymentIntent = await paymentIntentService.CreateAsync(stripeOptions);

      string paymentIntentId = paymentIntent.Id;

      order.StripePaymentIntentId = paymentIntentId;

      var res = await _supabase
        .From<Order>()
        .Insert(order);

      if (res.Models is null || res.Models.Count == 0)
        return BadRequest("Could not process order");

      return CreatedAtAction(
        nameof(GetOrder), 
        new { id = res.Models.First().Id }, 
        res.Models.First()
      );
    }
  }
}