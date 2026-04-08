using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.Persistence.Context;
using StockX.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StockX.Tests.UnitTests.Repositories;

public sealed class UserRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new UserRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── GetByEmailAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsUser()
    {
        var user = Seed("alice@example.com");

        var result = await _sut.GetByEmailAsync("alice@example.com");

        result.Should().NotBeNull();
        result!.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task GetByEmailAsync_MissingEmail_ReturnsNull()
    {
        var result = await _sut.GetByEmailAsync("nobody@example.com");
        result.Should().BeNull();
    }

    // ── EmailExistsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task EmailExistsAsync_ExistingEmail_ReturnsTrue()
    {
        Seed("alice@example.com");

        var result = await _sut.EmailExistsAsync("alice@example.com");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmailExistsAsync_MissingEmail_ReturnsFalse()
    {
        var result = await _sut.EmailExistsAsync("nobody@example.com");
        result.Should().BeFalse();
    }

    private User Seed(string email)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Name = "Test",
            Email = email,
            PasswordHash = "hash",
            Role = UserRole.NormalUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
        return user;
    }
}
