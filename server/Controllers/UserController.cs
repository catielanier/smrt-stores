using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using DBUser = SmrtStores.Models.User;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;  
using Supabase;

namespace SmrtStores.Controllers
{
  [ApiController]
  [Route("api/users")]
  public class UserController : ControllerBase
  {
    private readonly Client _supabase;
    private readonly TokenService _tokenService;

    public UserController(Client supabase, TokenService tokenService)
    {
      _supabase = supabase;
      _tokenService = tokenService;
    }

    [HttpPost("signup")]
    public async Task<ActionResult<DBUser>> DoSignup(DBUser user)
    {
      var hashed = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
      user.PasswordHash = hashed;
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
      var token = _tokenService.GenerateToken(user);
      return Ok(new { token });
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

        if (expiryDate < now)
        {
            // Token is expired but valid — issue a new one
            var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!Guid.TryParse(userId, out Guid id))
                return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

            // Get user from DB
            var userReq = _supabase.From<DBUser>().Where(u => u.Id == id).Get().Result;

            if (userReq.Models.Count == 0)
                return Unauthorized(new { error = "JWT_SIGNING_ERROR" });

            var user = userReq.Models.First();

            var newToken = _tokenService.GenerateToken(user);

            return Ok(new { token = newToken });
        }

        // Token is valid and not expired
        return Ok(new { success = true });
    }
  }
}