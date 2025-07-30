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

    public UserController(Client supabase)
    {
      _supabase = supabase;
    }
    [HttpPost("signup")]
    public async Task<ActionResult<DBUser>> DoSignup(DBUser user)
    {
      var hashed = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
      user.PasswordHash = hashed;
      var res = await _supabase.From<DBUser>().Insert(user);
      if(res.Models.Count == 0)
        return BadRequest("User creation failed")
      
      return Ok(res.Models.First());
    }
  }
}