using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StockX.Core.Enums;
using StockX.Infrastructure.External.AlpacaApi.Models;
using StockX.Infrastructure.Persistence.Context;
using StockX.Core.Entities;
using Xunit;

namespace StockX.Tests.IntegrationTests;

public sealed class TradingControllerTests2 : IClassFixture<CustomWebApplicationFactory>
{
    // Supplementary tests targeting the Portfolio endpoint (covered via PortfolioController)
    private readonly CustomWebApplicationFactory _factory;

    public TradingControllerTests2(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/portfolio ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPortfolio_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/portfolio");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPortfolio_NoHoldings_Returns200WithEmptyHoldings()
    {
        var user = _factory.SeedUser(email: $"port_empty_{Guid.NewGuid()}@example.com");
        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/portfolio");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("holdings");
        json.Should().Contain("totalValue");
    }

    [Fact]
    public async Task GetPortfolio_WithHoldings_Returns200WithPositions()
    {
        var user = _factory.SeedUser(email: $"port_ok_{Guid.NewGuid()}@example.com");
        SeedHolding(user.UserId, "TSLA", 5m, 200m);

        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["TSLA"] = new AlpacaSnapshot
                {
                    LatestTrade = new SnapshotTrade { Price = 250m },
                    DailyBar = new SnapshotBar { Close = 250m, Timestamp = DateTime.UtcNow },
                    PrevDailyBar = new SnapshotBar { Close = 245m }
                }
            });

        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/portfolio");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("TSLA");
        json.Should().Contain("totalValue");
    }

    private void SeedHolding(Guid userId, string symbol, decimal qty, decimal avgCost)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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
