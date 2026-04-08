using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Services.Wallet;
using Xunit;
using StockX.Core.DTOs.Wallet;

namespace StockX.Tests.UnitTests.Services;

public sealed class WalletServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITransactionRepository> _transactionRepoMock;
    private readonly Mock<IRepository<Transaction>> _transactionsRepoMock;
    private readonly WalletService _sut;

    public WalletServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionRepoMock = new Mock<ITransactionRepository>();
        _transactionsRepoMock = new Mock<IRepository<Transaction>>();

        _unitOfWorkMock.Setup(u => u.Transactions).Returns(_transactionsRepoMock.Object);

        _sut = new WalletService(_unitOfWorkMock.Object, _transactionRepoMock.Object);
    }

    // ── GetWalletBalanceAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetWalletBalanceAsync_NoTransactions_ReturnsZeroBalanceAndMinDate()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _transactionsRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transaction>());

        // Act
        var result = await _sut.GetWalletBalanceAsync(userId);

        // Assert
        result.Balance.Should().Be(0m);
        result.LastUpdated.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public async Task GetWalletBalanceAsync_MultipleTransactions_SumsAmountsAndReturnsMaxTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var earlier = now.AddHours(-1);

        var transactions = new List<Transaction>
        {
            new Transaction { UserId = userId, Amount = 1000m, Status = TransactionStatus.Completed, Timestamp = earlier },
            new Transaction { UserId = userId, Amount = -200m, Status = TransactionStatus.Completed, Timestamp = now }
        };

        _transactionsRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _sut.GetWalletBalanceAsync(userId);

        // Assert
        result.Balance.Should().Be(800m);  // 1000 + (-200)
        result.LastUpdated.Should().Be(now);
    }

    // ── CalculateWalletBalanceAsync ────────────────────────────────────────────

    [Fact]
    public async Task CalculateWalletBalanceAsync_NoTransactions_ReturnsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _transactionsRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transaction>());

        // Act
        var result = await _sut.CalculateWalletBalanceAsync(userId);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateWalletBalanceAsync_MultipleTransactions_SumsAmounts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var transactions = new List<Transaction>
        {
            new Transaction { Amount = 500m, Status = TransactionStatus.Completed },
            new Transaction { Amount = 300m, Status = TransactionStatus.Completed },
            new Transaction { Amount = -100m, Status = TransactionStatus.Completed }
        };

        _transactionsRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _sut.CalculateWalletBalanceAsync(userId);

        // Assert
        result.Should().Be(700m); // 500 + 300 - 100
    }

    // ── GetTransactionsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionsAsync_DelegatesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expected = new List<Transaction>
        {
            new Transaction { UserId = userId, Type = TransactionType.Deposit, Amount = 100m }
        };

        _transactionRepoMock
            .Setup(r => r.GetByUserAsync(userId, TransactionType.Deposit, 10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.GetTransactionsAsync(userId, TransactionType.Deposit, 10, 0);

        // Assert
        result.Should().BeSameAs(expected);
        _transactionRepoMock.Verify(
            r => r.GetByUserAsync(userId, TransactionType.Deposit, 10, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTransactionsAsync_NullType_DelegatesWithNullType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expected = new List<Transaction>();

        _transactionRepoMock
            .Setup(r => r.GetByUserAsync(userId, null, 20, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.GetTransactionsAsync(userId, null, 20, 0);

        // Assert
        result.Should().BeSameAs(expected);
    }
}
