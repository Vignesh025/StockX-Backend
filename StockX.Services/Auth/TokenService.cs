using System.Security.Cryptography;
using System.Text;
using StockX.Core.Entities;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Auth;

public sealed class TokenService : ITokenService
{
    public string GenerateToken(User user)
    {
        var data = $"{user.UserId}:{user.Email}:{DateTime.UtcNow:O}";
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

