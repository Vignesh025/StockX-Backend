using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.Persistence.Context;
using StockX.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StockX.Tests.UnitTests.Repositories;

public sealed class HoldingRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly HoldingRepository _sut;
    private readonly Guid _userId1 = Guid.NewGuid();
    private readonly Guid _userId2 = Guid.NewGuid();

    public HoldingRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new HoldingRepository(_dbContext);

        SeedUsers();
    }

    public void Dispose() => _dbContext.Dispose();

    private void SeedUsers()
    {
        _dbContext.Users.AddRange(
            new User { UserId = _userId1, Name = "User1", Email = "u1@test.com", PasswordHash = "h", Role = UserRole.NormalUser, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { UserId = _userId2, Name = "User2", Email = "u2@test.com", PasswordHash = "h", Role = UserRole.NormalUser, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        _dbContext.Stocks.AddRange(
            new Stock { Symbol = "AAPL", Name = "Apple", Exchange = "NASDAQ", AssetType = "US Equity" },
            new Stock { Symbol = "MSFT", Name = "Microsoft", Exchange = "NASDAQ", AssetType = "US Equity" }
        );
        _dbContext.SaveChanges();
    }

    // ── GetForUserAndSymbolAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetForUserAndSymbolAsync_ExistingHolding_ReturnsHolding()
    {
        _dbContext.UserStockHoldings.Add(new UserStockHolding
        {
            UserId = _userId1,
            StockSymbol = "AAPL",
            TotalQuantity = 10m,
            AverageCostBasis = 150m,
            LastUpdated = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetForUserAndSymbolAsync(_userId1, "AAPL");

        result.Should().NotBeNull();
        result!.TotalQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task GetForUserAndSymbolAsync_WrongUser_ReturnsNull()
    {
        _dbContext.UserStockHoldings.Add(new UserStockHolding
        {
            UserId = _userId1,
            StockSymbol = "AAPL",
            TotalQuantity = 5m,
            AverageCostBasis = 100m,
            LastUpdated = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Looking up _userId2 who doesn't have AAPL
        var result = await _sut.GetForUserAndSymbolAsync(_userId2, "AAPL");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetForUserAndSymbolAsync_WrongSymbol_ReturnsNull()
    {
        _dbContext.UserStockHoldings.Add(new UserStockHolding
        {
            UserId = _userId1,
            StockSymbol = "AAPL",
            TotalQuantity = 5m,
            AverageCostBasis = 100m,
            LastUpdated = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetForUserAndSymbolAsync(_userId1, "MSFT");

        result.Should().BeNull();
    }

    // ── GetByUserAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserAsync_NoHoldings_ReturnsEmpty()
    {
        var result = await _sut.GetByUserAsync(_userId1);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserAsync_MultipleHoldings_ReturnsAllSortedBySymbol()
    {
        _dbContext.UserStockHoldings.AddRange(
            new UserStockHolding { UserId = _userId1, StockSymbol = "MSFT", TotalQuantity = 5m, AverageCostBasis = 300m, LastUpdated = DateTime.UtcNow },
            new UserStockHolding { UserId = _userId1, StockSymbol = "AAPL", TotalQuantity = 10m, AverageCostBasis = 150m, LastUpdated = DateTime.UtcNow }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByUserAsync(_userId1);

        result.Should().HaveCount(2);
        result[0].StockSymbol.Should().Be("AAPL");  // sorted ascending
        result[1].StockSymbol.Should().Be("MSFT");
    }

    [Fact]
    public async Task GetByUserAsync_OnlyReturnsHoldingsForRequestedUser()
    {
        _dbContext.UserStockHoldings.AddRange(
            new UserStockHolding { UserId = _userId1, StockSymbol = "AAPL", TotalQuantity = 5m, AverageCostBasis = 100m, LastUpdated = DateTime.UtcNow },
            new UserStockHolding { UserId = _userId2, StockSymbol = "MSFT", TotalQuantity = 3m, AverageCostBasis = 200m, LastUpdated = DateTime.UtcNow }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByUserAsync(_userId1);

        result.Should().HaveCount(1);
        result[0].StockSymbol.Should().Be("AAPL");
    }
}
