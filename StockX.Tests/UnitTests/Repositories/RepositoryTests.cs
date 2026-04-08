using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.Persistence.Context;
using StockX.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StockX.Tests.UnitTests.Repositories;

/// <summary>
/// Base helper that creates a fresh in-memory ApplicationDbContext for every test.
/// Each test gets its own unique database name so tests never share state.
/// </summary>
public abstract class RepositoryTestBase : IDisposable
{
    protected readonly ApplicationDbContext DbContext;

    protected RepositoryTestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        DbContext = new ApplicationDbContext(options);
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }
}

/// <summary>
/// Tests for the shared <see cref="Repository{TEntity}"/> base class
/// exercised through <see cref="UserRepository"/>.
/// </summary>
public sealed class RepositoryTests : RepositoryTestBase
{
    private readonly Repository<User> _repo;

    public RepositoryTests()
    {
        _repo = new UserRepository(DbContext);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsEntity()
    {
        var user = CreateUser("alice@example.com");
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();

        var result = await _repo.GetByIdAsync(user.UserId);

        result.Should().NotBeNull();
        result!.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        DbContext.Users.AddRange(CreateUser("a@example.com"), CreateUser("b@example.com"));
        await DbContext.SaveChangesAsync();

        var result = await _repo.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAsync_Matches_ReturnsFilteredEntities()
    {
        var user1 = CreateUser("active@example.com", isActive: true);
        var user2 = CreateUser("inactive@example.com", isActive: false);
        DbContext.Users.AddRange(user1, user2);
        await DbContext.SaveChangesAsync();

        var result = await _repo.FindAsync(u => u.IsActive);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("active@example.com");
    }

    [Fact]
    public async Task FindAsync_NoMatch_ReturnsEmptyList()
    {
        DbContext.Users.Add(CreateUser("alice@example.com", isActive: false));
        await DbContext.SaveChangesAsync();

        var result = await _repo.FindAsync(u => u.IsActive);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_AddsEntity()
    {
        var user = CreateUser("new@example.com");

        await _repo.AddAsync(user);
        await DbContext.SaveChangesAsync();

        DbContext.Users.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddRangeAsync_AddsMultipleEntities()
    {
        var users = new[]
        {
            CreateUser("one@example.com"),
            CreateUser("two@example.com")
        };

        await _repo.AddRangeAsync(users);
        await DbContext.SaveChangesAsync();

        DbContext.Users.Should().HaveCount(2);
    }

    [Fact]
    public async Task Update_ModifiesEntity()
    {
        var user = CreateUser("alice@example.com", isActive: true);
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();

        user.IsActive = false;
        _repo.Update(user);
        await DbContext.SaveChangesAsync();

        var updated = await DbContext.Users.FindAsync(user.UserId);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_DeletesEntity()
    {
        var user = CreateUser("alice@example.com");
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();

        _repo.Remove(user);
        await DbContext.SaveChangesAsync();

        DbContext.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRange_DeletesMultipleEntities()
    {
        var users = new[]
        {
            CreateUser("one@example.com"),
            CreateUser("two@example.com")
        };
        DbContext.Users.AddRange(users);
        await DbContext.SaveChangesAsync();

        _repo.RemoveRange(users);
        await DbContext.SaveChangesAsync();

        DbContext.Users.Should().BeEmpty();
    }

    private static User CreateUser(string email, bool isActive = true) => new()
    {
        UserId = Guid.NewGuid(),
        Name = "Test",
        Email = email,
        PasswordHash = "hash",
        Role = UserRole.NormalUser,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
