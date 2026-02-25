using StockX.Core.Entities;
using StockX.Core.Enums;

namespace StockX.Core.Interfaces;

public interface IAuthService
{
    Task<User> RegisterAsync(string name, string email, string password, CancellationToken cancellationToken = default);

    Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken = default);

    Task UpdateUserRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
}

