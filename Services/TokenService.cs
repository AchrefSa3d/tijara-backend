using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TijaraApi.Services;

public class TokenService
{
    private readonly string _secret;
    private readonly int    _expiresInDays;

    public TokenService(IConfiguration config)
    {
        _secret        = config["Jwt:Secret"] ?? "tijara_secret_key_2026_very_secure";
        _expiresInDays = int.Parse(config["Jwt:ExpiresInDays"] ?? "7");
    }

    public string GenerateToken(long id, string email, string role, string firstName, string lastName)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("id",        id.ToString()),
            new Claim("email",     email),
            new Claim("role",      role),
            new Claim("firstName", firstName),
            new Claim("lastName",  lastName),
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role,           role),
        };

        var token = new JwtSecurityToken(
            claims:   claims,
            expires:  DateTime.UtcNow.AddDays(_expiresInDays),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
