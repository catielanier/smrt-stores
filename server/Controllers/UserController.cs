using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using DBUser = SmrtStores.Models.User;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Supabase;
using Stripe;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/users")]
  public class UserController : ControllerBase
  {
    private readonly Client _supabase;
    private readonly TokenService _tokenService;
    private readonly StripeClient _stripeClient;

    public UserController(Client supabase, TokenService tokenService, StripeClient stripeClient)
    {
      _supabase = supabase;
      _tokenService = tokenService;
      _stripeClient = stripeClient;
    }

    [HttpPost("signup")]
    public async Task<ActionResult<DBUser>> DoSignup(DBUser user)
    {
      var hashed = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
      user.PasswordHash = hashed;
      var customerService = new CustomerService(_stripeClient);
      var stripeCustomerOptions = new CustomerCreateOptions
      {
        Email = user.Email
      };
      try 
      {
        Customer customer = await customerService.CreateAsync(stripeCustomerOptions);
        user.StripeCustomerId = customer.Id;
      }
      catch
      {
        return StatusCode(500, "Stripe customer creation failure");
      }
      var res = await _supabase.From<DBUser>().Insert(user);
      if(res.Models.Count == 0)
        return BadRequest("User creation failed");

      return Ok(res.Models.First());
    }

    [HttpPost("login")]
    public async Task<ActionResult<DBUser>> DoLogin(string email, string password)
    {
      var req = await _supabase.From<DBUser>()
        .Where(user => user.Email == email)
        .Get();
      if (req.Models is null || req.Models.Count == 0) {
        return BadRequest("Invalid email or password");
      }
      var user = req.Models.First();
      bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
      if (isValid == false) {
        return BadRequest("Invalid email or password");
      }
      bool showAdminPanel = user.Role == Models.Role.Owner;
      var token = _tokenService.GenerateToken(user);
      return Ok(new { token, showAdminPanel });
    }

    [HttpPost("init")]
    public ActionResult<object> Init()
    {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

        if (string.IsNullOrWhiteSpace(token))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

        var principal = _tokenService.ValidateToken(token);

        if (principal is null)
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

        // Get expiration claim
        var expClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

        if (expClaim is null || !long.TryParse(expClaim.Value, out long expUnix))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

        var expiryDate = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        var now = DateTime.UtcNow;

        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userId, out Guid id))
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

        var userReq = _supabase.From<DBUser>().Where(u => u.Id == id).Get().Result;

        if (userReq.Models.Count == 0)
          return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

        var user = userReq.Models.First();

      bool showAdminPanel = user.Role == Models.Role.Owner;

      if (expiryDate < now)
        {
          var newToken = _tokenService.GenerateToken(user);

          return Ok(new { token = newToken, showAdminPanel });
        }

        // Token is valid and not expired
        return Ok(new { success = true, showAdminPanel });
    }
  }
}