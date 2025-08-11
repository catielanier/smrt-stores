using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using SmrtStores.Models;
using DBUser = SmrtStores.Models.User;
using LocalTokenService = SmrtStores.Services.TokenService;
using Supabase;
using Stripe;
using SmrtStores.Dtos;
using SmrtStores.Services;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/orders")]
  public class OrderController : ControllerBase
  {
    private readonly Client _supabase;
    private readonly LocalTokenService _tokenService;
    private readonly StripeClient _stripeClient;
    private readonly ShippingService _shippingService;
    public OrderController(Client supabase, LocalTokenService tokenService, StripeClient stripeClient, ShippingService shippingService)
    {
      _supabase = supabase;
      _tokenService = tokenService;
      _stripeClient = stripeClient;
      _shippingService = shippingService;
    }

    [HttpGet()]
    public async Task<ActionResult<IEnumerable<OrderGetDto>>> GetOrders()
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

      var loggedInUser = await _supabase
        .From<DBUser>()
        .Where(u => u.Id == userId)
        .Get();

      if (loggedInUser.Models is null || loggedInUser.Models.Count == 0)
        return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      if (loggedInUser.Models.First().Role != Role.Owner)
        return Unauthorized(new { error = "Not authorized to view user's information." });
      var res = await _supabase.From<Order>().Get();
      List<OrderGetDto> orders = res.Models
        .Select(o => new OrderGetDto
        {
          OrderNumber = o.OrderNumber,
          TotalCents = o.TotalCents,
          Currency = o.Currency,
          ShippingAddress = o.ShippingAddress,
          ShippingMethod = o.ShippingMethod,
          ShippingStatus = o.ShippingStatus,
          TrackingNumber = o.TrackingNumber,
          TrackingUrl = o.TrackingUrl,
          ShippingCost = o.ShippingCost,
          CreatedAt = o.CreatedAt,
        }).ToList();
      return Ok(orders);
    }

    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<OrderGetDto>> GetOrder([FromRoute] string orderNumber)
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

      var loggedInUser = await _supabase
        .From<DBUser>()
        .Where(u => u.Id == userId)
        .Get();

      if (loggedInUser.Models is null || loggedInUser.Models.Count == 0)
        return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      var res = await _supabase
        .From<Order>()
        .Where(o => o.OrderNumber == orderNumber)
        .Get();

      if (res.Models is null || res.Models.Count == 0)
        return NotFound();

      var returnedOrder = res.Models.First();

      if (loggedInUser.Models.First().Role != Role.Owner || returnedOrder.UserId != loggedInUser.Models.First().Id)
        return Unauthorized(new { error = "Not authorized to view user's information." });

      var orderItems = await _supabase
        .From<OrderItem>()
        .Where(i => i.OrderId == returnedOrder.Id)
        .Get();

      OrderGetDto order = new OrderGetDto
      {
        OrderNumber = returnedOrder.OrderNumber,
        TotalCents = returnedOrder.TotalCents,
        Currency = returnedOrder.Currency,
        ShippingAddress = returnedOrder.ShippingAddress,
        ShippingMethod = returnedOrder.ShippingMethod,
        ShippingStatus = returnedOrder.ShippingStatus,
        TrackingNumber = returnedOrder.TrackingNumber,
        TrackingUrl = returnedOrder.TrackingUrl,
        ShippingCost = returnedOrder.ShippingCost,
        Items = orderItems.Models,
        CreatedAt = returnedOrder.CreatedAt,
      };

      return Ok(order);
    }

    [HttpGet("by-user/{id}")]
    public async Task<ActionResult<IEnumerable<OrderGetDto>>> GetOrdersByUser([FromRoute] Guid id)
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

      var loggedInUser = await _supabase
        .From<DBUser>()
        .Where(u => u.Id == userId)
        .Get();

      if (loggedInUser.Models is null || loggedInUser.Models.Count == 0)
        return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      if (loggedInUser.Models.First().Role != Role.Owner)
        return Unauthorized(new { error = "Not authorized to view user's information." });
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
      List<OrderGetDto> orders = res.Models
        .Select(o => new OrderGetDto
        {
          OrderNumber = o.OrderNumber,
          TotalCents = o.TotalCents,
          Currency = o.Currency,
          ShippingAddress = o.ShippingAddress,
          ShippingMethod = o.ShippingMethod,
          ShippingStatus = o.ShippingStatus,
          TrackingNumber = o.TrackingNumber,
          TrackingUrl = o.TrackingUrl,
          ShippingCost = o.ShippingCost,
          CreatedAt = o.CreatedAt,
        }).ToList();
      return Ok(orders);
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

      List<OrderGetDto> orders = res.Models
        .Select(o => new OrderGetDto
        {
          OrderNumber = o.OrderNumber,
          TotalCents = o.TotalCents,
          Currency = o.Currency,
          ShippingAddress = o.ShippingAddress,
          ShippingMethod = o.ShippingMethod,
          ShippingStatus = o.ShippingStatus,
          TrackingNumber = o.TrackingNumber,
          TrackingUrl = o.TrackingUrl,
          ShippingCost = o.ShippingCost,
          CreatedAt = o.CreatedAt,
        }).ToList();

      return Ok(orders);
    }

    [HttpPost()]
    public async Task<ActionResult<Order>> CreateOrder(OrderCreateDto dto)
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

      var user = await _supabase
        .From<DBUser>()
        .Where(u => u.Id == userId)
        .Get();

      int orderCount = await _supabase.From<Order>().Count(Supabase.Postgrest.Constants.CountType.Exact);

      Order order = new Order
      {
        UserId = (Guid)userId,
        TotalCents = dto.TotalCents,
        ShippingAddress = dto.ShippingAddress,
        ShippingMethod = dto.ShippingMethod,
        ShippingCost = dto.ShippingCost,
        OrderNumber = $"SMRT-{orderCount + 1:D6}",
      };

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

      var createdOrder = res.Models.FirstOrDefault();

      foreach(var item in dto.Items)
      {
        OrderItem orderItem = new OrderItem
        {
          OrderId = createdOrder!.Id,
          ProductId = item.ProductId,
          PriceCents = item.PriceCents,
          Quantity = item.Quantity,
        };

        await _supabase.From<OrderItem>().Insert(orderItem);
      }

      return CreatedAtAction(
        nameof(GetOrder), 
        new { id = createdOrder!.Id }, 
        createdOrder
      );
    }

    [HttpPost("shipping")]
    public async Task<ActionResult<List<ShippingReturnDto>>> CreateShippingQuotes([FromBody] ShippingCreateDto shippingQuote)
    {
      if (shippingQuote == null)
        return BadRequest("Missing shipping request.");
      if (string.IsNullOrWhiteSpace(shippingQuote.PostalCode) || string.IsNullOrWhiteSpace(shippingQuote.CountryCode))
        return BadRequest("Postal code and country are required.");

      // helper: try a provider, return empty list on failure
      static async Task<List<ShippingReturnDto>> TryGet(Func<Task<List<ShippingReturnDto>>> fn)
      {
        try { return await fn(); } catch { return new List<ShippingReturnDto>(); }
      }

      // fire everything in parallel
      var canadaPostTask = TryGet(() => _shippingService.GenerateCanadaPostShippingQuote(shippingQuote));
      var upsTask        = TryGet(() => _shippingService.GenerateUpsShippingQuote(shippingQuote));
      var fedexTask      = TryGet(() => _shippingService.GenerateFedexShippingQuote(shippingQuote));
      var purolatorTask  = TryGet(() => _shippingService.GeneratePurolatorShippingQuote(shippingQuote)); // returns [] if non-CA

      await Task.WhenAll(canadaPostTask, upsTask, fedexTask, purolatorTask);

      var all = new List<ShippingReturnDto>();
      all.AddRange(canadaPostTask.Result);
      all.AddRange(upsTask.Result);
      all.AddRange(fedexTask.Result);
      all.AddRange(purolatorTask.Result);

      // optional: de-dupe within same carrier+type+cost (some carriers return duplicate rate lines)
      var deduped = all
        .GroupBy(x => new { x.ShippingMethod, x.ShippingType, x.ShippingCost, x.Currency, x.ApproxShippingDaysMin, x.ApproxShippingDaysMax })
        .Select(g => g.First())
        .OrderBy(r => r.ShippingCost)
        .ToList();

      return Ok(deduped);
    }

    [HttpPut("{orderNumber}")]
    public async Task<ActionResult<OrderCreateDto>> UpdateOrder([FromRoute] string orderNumber, [FromBody] OrderUpdateDto dto)
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

      var loggedInUser = await _supabase
        .From<DBUser>()
        .Where(u => u.Id == userId)
        .Get();

      if (loggedInUser.Models is null || loggedInUser.Models.Count == 0)
        return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

      if (loggedInUser.Models.First().Role != Role.Owner)
        return Unauthorized(new { error = "Not authorized to view user's information." });

      var res = await _supabase.From<Order>().Where(o => o.OrderNumber == orderNumber).Get();

      if (res.Models is null || res.Models.Count == 0)
        return NotFound("No order found by order number");

      var order = res.Models.First();

      order.Status = dto.Status;
      order.TrackingNumber = dto.TrackingNumber;
      order.TrackingUrl = dto.TrackingUrl;

      var updatedOrder = await _supabase.From<Order>().Where(o => o.Id == order.Id).Update(order);

      if (updatedOrder.Models is null || updatedOrder.Models.Count == 0)
        return BadRequest("ORDER_UPDATE_ERROR");

      return Ok(updatedOrder.Models.First());
    }
  }
}