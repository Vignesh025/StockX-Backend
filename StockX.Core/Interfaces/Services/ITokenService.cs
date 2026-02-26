using StockX.Core.Entities;

namespace StockX.Core.Services.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}

