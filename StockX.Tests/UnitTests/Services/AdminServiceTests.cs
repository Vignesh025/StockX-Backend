using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using StockX.Core.DTOs.Admin;
using StockX.Core.DTOs.Common;
using StockX.Core.DTOs.Portfolio;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;
using StockX.Services.Admin;
using Xunit;

namespace StockX.Tests.UnitTests.Services;

public sealed class AdminServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IWalletService> _walletServiceMock;
    private readonly Mock<ITradingService> _tradingServiceMock;
    private readonly Mock<ITransactionRepository> _transactionRepoMock;
    private readonly Mock<IRepository<User>> _userRepoMock;
    private readonly AdminService _sut;

    public AdminServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _walletServiceMock = new Mock<IWalletService>();
        _tradingServiceMock = new Mock<ITradingService>();
        _transactionRepoMock = new Mock<ITransactionRepository>();
        _userRepoMock = new Mock<IRepository<User>>();

        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _sut = new AdminService(
            _unitOfWorkMock.Object,
            _walletServiceMock.Object,
            _tradingServiceMock.Object,
            _transactionRepoMock.Object);
    }

    // ── GetUsersAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsersAsync_NoSearch_ReturnsPagedUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com", Role = UserRole.NormalUser, IsActive = true },
            new() { UserId = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com", Role = UserRole.Admin, IsActive = true }
        };

        _userRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);

        // Act
        var result = await _sut.GetUsersAsync(1, 10, null);

        // Assert
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUsersAsync_WithSearch_FiltersUsersByEmail()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com", Role = UserRole.NormalUser, IsActive = true },
            new() { UserId = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com", Role = UserRole.Admin, IsActive = true }
        };

        _userRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Act
        var result = await _sut.GetUsersAsync(1, 10, "alice");

        // Assert
        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task GetUsersAsync_PageLteZero_DefaultsToPage1()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.GetUsersAsync(0, 10, null);

        // Assert
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetUsersAsync_PageSizeLteZero_DefaultsTo20()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.GetUsersAsync(1, 0, null);

        // Assert
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetUsersAsync_SecondPage_ReturnsCorrectSlice()
    {
        // Arrange - 25 users, page=2, pageSize=10
        var users = Enumerable.Range(1, 25)
            .Select(i => new User
            {
                UserId = Guid.NewGuid(),
                Name = $"User{i:D2}",
                Email = $"user{i:D2}@example.com",
                Role = UserRole.NormalUser,
                IsActive = true
            })
            .ToList();

        _userRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Act
        var result = await _sut.GetUsersAsync(2, 10, null);

        // Assert
        result.Total.Should().Be(25);
        result.Items.Should().HaveCount(10);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetUsersAsync_WalletBalanceIsRetrievedPerUser()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var users = new List<User>
        {
            new() { UserId = userId1, Name = "Alice", Email = "a@example.com", Role = UserRole.NormalUser, IsActive = true },
            new() { UserId = userId2, Name = "Bob", Email = "b@example.com", Role = UserRole.NormalUser, IsActive = true }
        };

        _userRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);
        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);

        // Act
        var result = await _sut.GetUsersAsync(1, 10, null);

        // Assert — items are sorted by email so alice=500, bob=1000
        result.Items[0].WalletBalance.Should().Be(500m);
        result.Items[1].WalletBalance.Should().Be(1000m);
    }

    // ── GetUserDetailAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserDetailAsync_UserNotFound_ReturnsNull()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.GetUserDetailAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserDetailAsync_UserFound_ReturnsDetailWithPortfolioAndTransactions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Name = "Alice",
            Email = "alice@example.com",
            Role = UserRole.NormalUser,
            IsActive = true
        };

        var portfolio = new PortfolioSummary(
            new List<HoldingSummary>
            {
                new("AAPL", "Apple", 10, 100m, 120m, 1200m, 200m, 20m)
            },
            1200m, 1000m, 200m);

        var transactions = new List<Transaction>
        {
            new() { TransactionId = Guid.NewGuid(), Type = TransactionType.Deposit, Amount = 1000m }
        };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);

        _tradingServiceMock
            .Setup(t => t.GetPortfolioAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        _transactionRepoMock
            .Setup(r => r.GetRecentByUserAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _sut.GetUserDetailAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Name.Should().Be("Alice");
        result.WalletBalance.Should().Be(500m);
        result.PortfolioSummary.TotalValue.Should().Be(1200m);
        result.RecentTransactions.Should().HaveCount(1);
    }
}
