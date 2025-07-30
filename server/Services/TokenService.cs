using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using SmrtStores.Models;

public class TokenService
{
  private readonly string _secretKey;
  private readonly int _expiryDays;

  public TokenService(IConfiguration configuration)
  {
    _secretKey = configuration["JWT_SECRET"] ?? throw new Exception("non-existent JWT secret");
    _expiryDays = int.TryParse(configuration["JWT_EXPIRY"], out int days) ? days : 7;
  }
  public string GenerateToken(User user)
  {
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new Claim(JwtRegisteredClaimNames.Email, user.Email),
      new Claim("name", user.Name),
      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var token = new JwtSecurityToken(
      issuer: "smrtstores",
      audience: "smrtstores-client",
      claims: claims,
      expires: DateTime.UtcNow.AddDays(_expiryDays),
      signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
  public ClaimsPrincipal? ValidateToken(string token) {
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(_secretKey);

    try
    {
      var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidIssuer = "smrtstores",
        ValidateAudience = true,
        ValidAudience = "smrtstores-client",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.FromMinutes(1)
      }, out _);

      return principal;
    }
    catch
    {
      return null;
    }
  }
}