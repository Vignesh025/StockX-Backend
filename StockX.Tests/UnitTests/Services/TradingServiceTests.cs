using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using StockX.Core.DTOs.Stock;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;
using StockX.Services.Trading;
using Xunit;

namespace StockX.Tests.UnitTests.Services;

public sealed class TradingServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IHoldingRepository> _holdingRepoMock;
    private readonly Mock<ITransactionRepository> _transactionRepoMock;
    private readonly Mock<IWalletService> _walletServiceMock;
    private readonly Mock<IStockService> _stockServiceMock;
    private readonly Mock<IRepository<UserStockHolding>> _holdingsRepoMock;
    private readonly Mock<IRepository<Transaction>> _transactionsRepoMock;
    private readonly TradingService _sut;

    public TradingServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _holdingRepoMock = new Mock<IHoldingRepository>();
        _transactionRepoMock = new Mock<ITransactionRepository>();
        _walletServiceMock = new Mock<IWalletService>();
        _stockServiceMock = new Mock<IStockService>();
        _holdingsRepoMock = new Mock<IRepository<UserStockHolding>>();
        _transactionsRepoMock = new Mock<IRepository<Transaction>>();

        _unitOfWorkMock.Setup(u => u.Holdings).Returns(_holdingsRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Transactions).Returns(_transactionsRepoMock.Object);

        _sut = new TradingService(
            _unitOfWorkMock.Object,
            _holdingRepoMock.Object,
            _transactionRepoMock.Object,
            _walletServiceMock.Object,
            _stockServiceMock.Object);
    }

    // ── BuyAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuyAsync_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _sut.BuyAsync(Guid.NewGuid(), "AAPL", -1m);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task BuyAsync_ZeroQuantity_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _sut.BuyAsync(Guid.NewGuid(), "AAPL", 0m);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task BuyAsync_StockNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockQuote?)null);

        // Act
        var act = () => _sut.BuyAsync(Guid.NewGuid(), "AAPL", 1m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stock not found.");
    }

    [Fact]
    public async Task BuyAsync_InsufficientBalance_ReturnsFailedTradeResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var quote = new StockQuote("AAPL", "Apple", "NASDAQ", 180m, null, 1m, DateTime.UtcNow);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m); // balance < cost (180 * 2 = 360)

        // Act
        var result = await _sut.BuyAsync(userId, "AAPL", 2m);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Insufficient wallet balance.");
        result.NewBalance.Should().Be(50m);
    }

    [Fact]
    public async Task BuyAsync_NewHolding_CreatesHoldingAndTransaction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var quote = new StockQuote("AAPL", "Apple", "NASDAQ", 100m, null, 1m, DateTime.UtcNow);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);

        _holdingRepoMock
            .Setup(r => r.GetForUserAndSymbolAsync(userId, "AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserStockHolding?)null);

        _transactionsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _holdingsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<UserStockHolding>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.BuyAsync(userId, "AAPL", 2m);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Stock purchased successfully.");
        result.NewBalance.Should().Be(800m); // 1000 - (100 * 2)
        result.Transaction.Type.Should().Be(TransactionType.StockBuy);
        result.Transaction.Status.Should().Be(TransactionStatus.Completed);

        _holdingsRepoMock.Verify(r => r.AddAsync(It.IsAny<UserStockHolding>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuyAsync_ExistingHolding_UpdatesAverageCostAndQuantity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var quote = new StockQuote("AAPL", "Apple", "NASDAQ", 200m, null, 1m, DateTime.UtcNow);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5000m);

        var existing = new UserStockHolding
        {
            UserId = userId,
            StockSymbol = "AAPL",
            TotalQuantity = 10m,
            AverageCostBasis = 150m
        };

        _holdingRepoMock
            .Setup(r => r.GetForUserAndSymbolAsync(userId, "AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _transactionsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.BuyAsync(userId, "AAPL", 10m);

        // Assert
        result.Success.Should().BeTrue();
        existing.TotalQuantity.Should().Be(20m);   // 10 + 10
        existing.AverageCostBasis.Should().Be(175m); // (10*150 + 10*200) / 20

        _holdingsRepoMock.Verify(r => r.AddAsync(It.IsAny<UserStockHolding>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── SellAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SellAsync_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _sut.SellAsync(Guid.NewGuid(), "AAPL", -1m);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SellAsync_ZeroQuantity_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _sut.SellAsync(Guid.NewGuid(), "AAPL", 0m);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SellAsync_NoHolding_ReturnsFailedTradeResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _holdingRepoMock
            .Setup(r => r.GetForUserAndSymbolAsync(userId, "AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserStockHolding?)null);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockQuote?)null);

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);

        // Act
        var result = await _sut.SellAsync(userId, "AAPL", 5m);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Insufficient holdings to sell.");
    }

    [Fact]
    public async Task SellAsync_InsufficientHoldings_ReturnsFailedTradeResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var holding = new UserStockHolding
        {
            UserId = userId,
            StockSymbol = "AAPL",
            TotalQuantity = 2m
        };

        _holdingRepoMock
            .Setup(r => r.GetForUserAndSymbolAsync(userId, "AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(holding);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockQuote("AAPL", "Apple", "NASDAQ", 100m, null, 1m, DateTime.UtcNow));

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);

        // Act — try to sell 5 but only have 2
        var result = await _sut.SellAsync(userId, "AAPL", 5m);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Insufficient holdings to sell.");
    }

    [Fact]
    public async Task SellAsync_StockNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var holding = new UserStockHolding
        {
            UserId = userId,
            StockSymbol = "AAPL",
            TotalQuantity = 10m
        };

        _holdingRepoMock
            .Setup(r => r.GetForUserAndSymbolAsync(userId, "AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(holding);

        // quote is null → throws
        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockQuote?)null);

        // Act
        var act = () => _sut.SellAsync(userId, "AAPL", 5m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stock not found.");
    }

    [Fact]
    public async Task SellAsync_PartialSell_UpdatesHolding()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var holding = new UserStockHolding
        {
            UserId = userId,
            StockSymbol = "AAPL",
            TotalQuantity = 10m,
            AverageCostBasis = 100m
        };

        _holdingRepoMock
            .Setup(r => r.GetForUserAndSymbolAsync(userId, "AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(holding);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockQuote("AAPL", "Apple", "NASDAQ", 150m, null, 1m, DateTime.UtcNow));

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m + 150m * 3m); // after selling 3 shares at 150

        _transactionsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.SellAsync(userId, "AAPL", 3m);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Stock sold successfully.");
        holding.TotalQuantity.Should().Be(7m); // 10 - 3

        _holdingsRepoMock.Verify(r => r.Update(holding), Times.Once);
        _holdingsRepoMock.Verify(r => r.Remove(It.IsAny<UserStockHolding>()), Times.Never);
    }

    [Fact]
    public async Task SellAsync_SellAll_RemovesHolding()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var holding = new UserStockHolding
        {
            UserId = userId,
            StockSymbol = "AAPL",
            TotalQuantity = 5m,
            AverageCostBasis = 100m
        };

        _holdingRepoMock
            .Setup(r => r.GetForUserAndSymbolAsync(userId, "AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(holding);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockQuote("AAPL", "Apple", "NASDAQ", 120m, null, 1m, DateTime.UtcNow));

        _walletServiceMock
            .Setup(w => w.CalculateWalletBalanceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(600m);

        _transactionsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.SellAsync(userId, "AAPL", 5m); // sell all

        // Assert
        result.Success.Should().BeTrue();
        holding.TotalQuantity.Should().Be(0m);

        _holdingsRepoMock.Verify(r => r.Remove(holding), Times.Once);
        _holdingsRepoMock.Verify(r => r.Update(It.IsAny<UserStockHolding>()), Times.Never);
    }

    // ── GetPortfolioAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPortfolioAsync_NoHoldings_ReturnsEmptyPortfolio()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _holdingRepoMock
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserStockHolding>());

        // Act
        var result = await _sut.GetPortfolioAsync(userId);

        // Assert
        result.Holdings.Should().BeEmpty();
        result.TotalValue.Should().Be(0m);
        result.TotalCost.Should().Be(0m);
        result.TotalProfitLoss.Should().Be(0m);
    }

    [Fact]
    public async Task GetPortfolioAsync_WithHoldings_CalculatesProfitLossCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var holdings = new List<UserStockHolding>
        {
            new() { UserId = userId, StockSymbol = "AAPL", TotalQuantity = 10m, AverageCostBasis = 100m }
        };

        _holdingRepoMock
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holdings);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockQuote("AAPL", "Apple", "NASDAQ", 120m, null, 1m, DateTime.UtcNow));

        // Act
        var result = await _sut.GetPortfolioAsync(userId);

        // Assert
        result.Holdings.Should().HaveCount(1);
        result.TotalValue.Should().Be(1200m);       // 10 * 120
        result.TotalCost.Should().Be(1000m);         // 10 * 100
        result.TotalProfitLoss.Should().Be(200m);    // 1200 - 1000
    }

    [Fact]
    public async Task GetPortfolioAsync_QuoteIsNull_CurrentPriceDefaultsToZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var holdings = new List<UserStockHolding>
        {
            new() { UserId = userId, StockSymbol = "DELISTED", TotalQuantity = 5m, AverageCostBasis = 100m }
        };

        _holdingRepoMock
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holdings);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("DELISTED", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockQuote?)null);

        // Act
        var result = await _sut.GetPortfolioAsync(userId);

        // Assert
        result.Holdings[0].CurrentPrice.Should().Be(0m);
        result.TotalValue.Should().Be(0m);
        result.TotalProfitLoss.Should().Be(-500m); // 0 - 500
    }

    [Fact]
    public async Task GetPortfolioAsync_ZeroCost_ProfitLossPercentIsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var holdings = new List<UserStockHolding>
        {
            new() { UserId = userId, StockSymbol = "AAPL", TotalQuantity = 10m, AverageCostBasis = 0m }
        };

        _holdingRepoMock
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holdings);

        _stockServiceMock
            .Setup(s => s.GetStockDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockQuote("AAPL", "Apple", "NASDAQ", 120m, null, 1m, DateTime.UtcNow));

        // Act
        var result = await _sut.GetPortfolioAsync(userId);

        // Assert — CostBasis is 0, profitLossPercent must be 0 (no division by zero)
        result.Holdings[0].ProfitLossPercent.Should().Be(0m);
    }
}
