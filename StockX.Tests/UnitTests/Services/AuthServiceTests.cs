using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;
using StockX.Services.Auth;
using Xunit;

namespace StockX.Tests.UnitTests.Services;

public sealed class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IRepository<User>> _userRepoMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tokenServiceMock = new Mock<ITokenService>();
        _userRepoMock = new Mock<IRepository<User>>();

        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _sut = new AuthService(_unitOfWorkMock.Object, _tokenServiceMock.Object);
    }

    // ── RegisterAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesAndReturnsUser()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        _userRepoMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.RegisterAsync("Alice", "alice@example.com", "password123");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Alice");
        result.Email.Should().Be("alice@example.com");
        result.Role.Should().Be(UserRole.NormalUser);
        result.IsActive.Should().BeTrue();
        result.PasswordHash.Should().NotBe("password123"); // must be hashed
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var existing = new List<User> { new User { Email = "alice@example.com" } };
        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var act = () => _sut.RegisterAsync("Alice", "alice@example.com", "password123");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email is already registered.");
    }

    // ── LoginAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentialsActiveUser_ReturnsToken()
    {
        // Arrange
        const string password = "correct-password";
        const string token = "jwt-token-xyz";

        // We need the hashed password — use the same SHA256 approach as AuthService
        var hash = ComputeHash(password);
        var user = new User { Email = "alice@example.com", PasswordHash = hash, IsActive = true };

        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });

        _tokenServiceMock
            .Setup(t => t.GenerateToken(user))
            .Returns(token);

        // Act
        var result = await _sut.LoginAsync("alice@example.com", password);

        // Assert
        result.Should().Be(token);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsNull()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.LoginAsync("nobody@example.com", "pass");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Email = "alice@example.com",
            PasswordHash = ComputeHash("correct-pass"),
            IsActive = true
        };

        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });

        // Act
        var result = await _sut.LoginAsync("alice@example.com", "wrong-pass");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        // Arrange
        const string password = "correct-password";
        var user = new User
        {
            Email = "alice@example.com",
            PasswordHash = ComputeHash(password),
            IsActive = false
        };

        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });

        // Act
        var result = await _sut.LoginAsync("alice@example.com", password);

        // Assert
        result.Should().BeNull();
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetByIdAsync(userId);

        // Assert
        result.Should().Be(user);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // ── GetByEmailAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsUser()
    {
        // Arrange
        var user = new User { Email = "alice@example.com" };

        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });

        // Act
        var result = await _sut.GetByEmailAsync("alice@example.com");

        // Assert
        result.Should().Be(user);
    }

    [Fact]
    public async Task GetByEmailAsync_MissingEmail_ReturnsNull()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.GetByEmailAsync("nobody@example.com");

        // Assert
        result.Should().BeNull();
    }

    // ── IsEmailTakenAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsEmailTakenAsync_EmailExists_ReturnsTrue()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { new User() });

        // Act
        var result = await _sut.IsEmailTakenAsync("alice@example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailTakenAsync_EmailMissing_ReturnsFalse()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.IsEmailTakenAsync("nobody@example.com");

        // Assert
        result.Should().BeFalse();
    }

    // ── UpdateUserRoleAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserRoleAsync_ExistingUser_UpdatesRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, Role = UserRole.NormalUser };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _sut.UpdateUserRoleAsync(userId, UserRole.Admin);

        // Assert
        user.Role.Should().Be(UserRole.Admin);
        _userRepoMock.Verify(r => r.Update(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_MissingUser_ThrowsInvalidOperationException()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.UpdateUserRoleAsync(Guid.NewGuid(), UserRole.Admin);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User not found.");
    }

    // ── SetUserActiveStatusAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SetUserActiveStatusAsync_ExistingUser_UpdatesStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, IsActive = true };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _sut.SetUserActiveStatusAsync(userId, false);

        // Assert
        user.IsActive.Should().BeFalse();
        _userRepoMock.Verify(r => r.Update(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserActiveStatusAsync_MissingUser_ThrowsInvalidOperationException()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.SetUserActiveStatusAsync(Guid.NewGuid(), false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User not found.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string ComputeHash(string password)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
