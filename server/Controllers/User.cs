using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using DBUser = SmrtStores.Models.User;
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
  }
}