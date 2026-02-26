using System.Security.Cryptography;
using System.Text;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;

    public AuthService(
        IUnitOfWork unitOfWork,
        ITokenService tokenService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task<User> RegisterAsync(
        string name,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Users.FindAsync(
            u => u.Email == email,
            cancellationToken);

        if (existing.Count > 0)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = HashPassword(password),
            Role = UserRole.NormalUser,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<string?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.FindAsync(
            u => u.Email == email,
            cancellationToken);

        var user = users.FirstOrDefault();

        if (user is null || !VerifyPassword(password, user.PasswordHash) || !user.IsActive)
        {
            return null;
        }

        return _tokenService.GenerateToken(user);
    }

    public async Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.FindAsync(
            u => u.Email == email,
            cancellationToken);

        return users.FirstOrDefault();
    }

    public async Task<bool> IsEmailTakenAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.FindAsync(
            u => u.Email == email,
            cancellationToken);

        return users.Count > 0;
    }

    public async Task UpdateUserRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string HashPassword(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var computed = HashPassword(password);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(hash));
    }
}

