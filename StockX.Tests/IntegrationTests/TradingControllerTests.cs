using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StockX.Core.DTOs.Trading;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.External.AlpacaApi.Models;
using StockX.Infrastructure.Persistence.Context;
using Xunit;

namespace StockX.Tests.IntegrationTests;

public sealed class TradingControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TradingControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── POST /api/trading/buy ──────────────────────────────────────────────────

    [Fact]
    public async Task Buy_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/trading/buy",
            new { symbol = "AAPL", quantity = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Buy_InsufficientBalance_Returns200WithFailedResult()
    {
        // Arrange — user has no transactions → zero balance
        var user = _factory.SeedUser(email: $"buy_insuf_{Guid.NewGuid()}@example.com");
        var client = _factory.CreateAuthenticatedClient(user);

        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["AAPL"] = BuildSnapshot(9999m)   // expensive — user can't afford
            });

        // Act
        var response = await client.PostAsJsonAsync("/api/trading/buy",
            new { symbol = "AAPL", quantity = 1 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsAsync<TradeResponse>();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("Insufficient");
    }

    [Fact]
    public async Task Buy_StockNotFound_Returns500()
    {
        // Arrange — Alpaca returns no snapshot → stock not found → exception
        var user = _factory.SeedUser(email: $"buy_nf_{Guid.NewGuid()}@example.com");
        var client = _factory.CreateAuthenticatedClient(user);

        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>());

        // Act
        var response = await client.PostAsJsonAsync("/api/trading/buy",
            new { symbol = "FAKE", quantity = 1 });

        // Assert — ExceptionHandlingMiddleware wraps the InvalidOperationException → 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Buy_SufficientBalance_Returns200WithSuccess()
    {
        // Arrange — seed a deposit transaction to give the user a balance
        var user = _factory.SeedUser(email: $"buy_ok_{Guid.NewGuid()}@example.com");
        SeedDeposit(user.UserId, 10_000m);

        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["AAPL"] = BuildSnapshot(100m)
            });

        var client = _factory.CreateAuthenticatedClient(user);

        // Act
        var response = await client.PostAsJsonAsync("/api/trading/buy",
            new { symbol = "AAPL", quantity = 2 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsAsync<TradeResponse>();
        result!.Success.Should().BeTrue();
        result.Message.Should().Be("Stock purchased successfully.");
        result.NewBalance.Should().Be(9_800m); // 10_000 - (100 * 2)
    }

    // ── POST /api/trading/sell ─────────────────────────────────────────────────

    [Fact]
    public async Task Sell_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/trading/sell",
            new { symbol = "AAPL", quantity = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sell_NoHoldings_Returns200WithFailedResult()
    {
        var user = _factory.SeedUser(email: $"sell_nohld_{Guid.NewGuid()}@example.com");
        var client = _factory.CreateAuthenticatedClient(user);

        // Need a valid snapshot so the service doesn't throw "Stock not found"
        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["AAPL"] = BuildSnapshot(150m)
            });

        var response = await client.PostAsJsonAsync("/api/trading/sell",
            new { symbol = "AAPL", quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsAsync<TradeResponse>();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("Insufficient holdings");
    }

    [Fact]
    public async Task Sell_SufficientHoldings_Returns200WithSuccess()
    {
        // Arrange — buy first, then sell
        var user = _factory.SeedUser(email: $"sell_ok_{Guid.NewGuid()}@example.com");
        SeedDeposit(user.UserId, 10_000m);
        SeedHolding(user.UserId, "AAPL", 10m, 100m);

        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["AAPL"] = BuildSnapshot(120m)
            });

        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/trading/sell",
            new { symbol = "AAPL", quantity = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsAsync<TradeResponse>();
        result!.Success.Should().BeTrue();
        result.Message.Should().Be("Stock sold successfully.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AlpacaSnapshot BuildSnapshot(decimal price) =>
        new()
        {
            LatestTrade = new SnapshotTrade { Price = price },
            DailyBar = new SnapshotBar { Close = price, Timestamp = DateTime.UtcNow },
            PrevDailyBar = new SnapshotBar { Close = price - 5m }
        };

    private void SeedDeposit(Guid userId, decimal amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Transactions.Add(new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = userId,
            Type = TransactionType.Deposit,
            Amount = amount,
            Status = TransactionStatus.Completed,
            Timestamp = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private void SeedHolding(Guid userId, string symbol, decimal qty, decimal avgCost)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure stock exists (FK)
        if (db.Stocks.Find(symbol) is null)
        {
            db.Stocks.Add(new StockX.Core.Entities.Stock
            {
                Symbol = symbol,
                Name = $"{symbol} Corp",
                Exchange = "NASDAQ",
                AssetType = "US Equity"
            });
        }

        db.UserStockHoldings.Add(new UserStockHolding
        {
            UserId = userId,
            StockSymbol = symbol,
            TotalQuantity = qty,
            AverageCostBasis = avgCost,
            LastUpdated = DateTime.UtcNow
        });

        db.SaveChanges();
    }
}
