using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.Persistence.Context;
using StockX.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StockX.Tests.UnitTests.Repositories;

public sealed class TransactionRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TransactionRepository _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TransactionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new TransactionRepository(_dbContext);

        SeedUser();
    }

    public void Dispose() => _dbContext.Dispose();

    private void SeedUser()
    {
        _dbContext.Users.Add(new User
        {
            UserId = _userId,
            Name = "Test",
            Email = "test@test.com",
            PasswordHash = "h",
            Role = UserRole.NormalUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    // ── GetByUserAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserAsync_NoTransactions_ReturnsEmpty()
    {
        var result = await _sut.GetByUserAsync(_userId, null, 10, 0);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserAsync_NoTypeFilter_ReturnsAllUserTransactions()
    {
        SeedTransactions(
            (TransactionType.Deposit, 100m),
            (TransactionType.StockBuy, -50m),
            (TransactionType.StockSell, 60m)
        );

        var result = await _sut.GetByUserAsync(_userId, null, 50, 0);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByUserAsync_WithTypeFilter_ReturnsOnlyMatchingType()
    {
        SeedTransactions(
            (TransactionType.Deposit, 100m),
            (TransactionType.StockBuy, -50m),
            (TransactionType.Deposit, 200m)
        );

        var result = await _sut.GetByUserAsync(_userId, TransactionType.Deposit, 50, 0);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.Type.Should().Be(TransactionType.Deposit));
    }

    [Fact]
    public async Task GetByUserAsync_LimitApplied_ReturnsTruncatedResults()
    {
        SeedTransactions(
            (TransactionType.Deposit, 100m),
            (TransactionType.Deposit, 200m),
            (TransactionType.Deposit, 300m)
        );

        var result = await _sut.GetByUserAsync(_userId, null, 2, 0);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserAsync_OffsetApplied_SkipsFirstResults()
    {
        SeedTransactions(
            (TransactionType.Deposit, 100m),
            (TransactionType.Deposit, 200m),
            (TransactionType.Deposit, 300m)
        );

        var result = await _sut.GetByUserAsync(_userId, null, 10, 2);

        // offset 2 with 3 total → 1 result
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByUserAsync_LimitLteZero_DefaultsTo50()
    {
        // Add 60 transactions
        for (var i = 0; i < 60; i++)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = _userId,
                Type = TransactionType.Deposit,
                Amount = 10m,
                Status = TransactionStatus.Completed,
                Timestamp = DateTime.UtcNow.AddSeconds(-i)
            });
        }
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByUserAsync(_userId, null, 0, 0);

        result.Should().HaveCount(50); // default
    }

    [Fact]
    public async Task GetByUserAsync_NegativeOffset_DefaultsToZero()
    {
        SeedTransactions(
            (TransactionType.Deposit, 100m),
            (TransactionType.Deposit, 200m)
        );

        // Negative offset should not throw, and should treat as 0
        var result = await _sut.GetByUserAsync(_userId, null, 10, -5);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserAsync_IsSortedDescendingByTimestamp()
    {
        var oldest = DateTime.UtcNow.AddDays(-2);
        var newest = DateTime.UtcNow;

        _dbContext.Transactions.AddRange(
            new Transaction { TransactionId = Guid.NewGuid(), UserId = _userId, Type = TransactionType.Deposit, Amount = 10m, Status = TransactionStatus.Completed, Timestamp = oldest },
            new Transaction { TransactionId = Guid.NewGuid(), UserId = _userId, Type = TransactionType.Deposit, Amount = 20m, Status = TransactionStatus.Completed, Timestamp = newest }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByUserAsync(_userId, null, 10, 0);

        result[0].Timestamp.Should().Be(newest);
        result[1].Timestamp.Should().Be(oldest);
    }

    // ── GetRecentByUserAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetRecentByUserAsync_NoTransactions_ReturnsEmpty()
    {
        var result = await _sut.GetRecentByUserAsync(_userId, 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentByUserAsync_LimitApplied_ReturnsMostRecent()
    {
        SeedTransactions(
            (TransactionType.Deposit, 100m),
            (TransactionType.Deposit, 200m),
            (TransactionType.Deposit, 300m)
        );

        var result = await _sut.GetRecentByUserAsync(_userId, 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentByUserAsync_LimitLteZero_DefaultsTo10()
    {
        // Seed 15 transactions
        for (var i = 0; i < 15; i++)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = _userId,
                Type = TransactionType.Deposit,
                Amount = 10m,
                Status = TransactionStatus.Completed,
                Timestamp = DateTime.UtcNow.AddSeconds(-i)
            });
        }
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentByUserAsync(_userId, 0);

        result.Should().HaveCount(10); // default
    }

    private void SeedTransactions(params (TransactionType type, decimal amount)[] entries)
    {
        var now = DateTime.UtcNow;
        foreach (var (type, amount) in entries)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = _userId,
                Type = type,
                Amount = amount,
                Status = TransactionStatus.Completed,
                Timestamp = now
            });
            now = now.AddSeconds(1); // ensure unique timestamps
        }
        _dbContext.SaveChanges();
    }
}
