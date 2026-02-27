using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StockX.Core.Entities;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Auth;

public sealed class TokenService : ITokenService
{
    private readonly string _secret;
    private readonly int _expirationHours;

    public TokenService(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"] ??
                  configuration["JWT_SECRET"] ??
                  "development-secret-key";

        _expirationHours = int.TryParse(
            configuration["Jwt:ExpirationHours"] ?? configuration["JWT_EXPIRATION_HOURS"],
            out var hours)
            ? hours
            : 24;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_expirationHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

