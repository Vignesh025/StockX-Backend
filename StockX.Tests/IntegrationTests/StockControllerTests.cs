using System.Net;
using FluentAssertions;
using Moq;
using StockX.Core.DTOs.Stock;
using StockX.Infrastructure.External.AlpacaApi.Models;
using Xunit;

namespace StockX.Tests.IntegrationTests;

public sealed class StockControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StockControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/stock/search ──────────────────────────────────────────────────

    [Fact]
    public async Task Search_DbEmpty_AlpacaReturnsResults_Returns200WithStocks()
    {
        // Arrange
        _factory.AlpacaMock
            .Setup(a => a.SearchAssetsAsync("AAPL", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlpacaAsset>
            {
                new() { Symbol = "AAPL", Name = "Apple Inc.", Exchange = "NASDAQ" }
            });

        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>());

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/stock/search?query=AAPL&limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadAsAsync<List<StockDto>>();
        results.Should().NotBeNull();
        results!.Should().HaveCountGreaterThan(0);
        results![0].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task Search_AlpacaThrows_StillReturns200WithEmptyList()
    {
        // Arrange
        _factory.AlpacaMock
            .Setup(a => a.SearchAssetsAsync("XYZ", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Alpaca unavailable"));

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/stock/search?query=XYZ&limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadAsAsync<List<StockDto>>();
        results.Should().NotBeNull();
        results!.Should().BeEmpty();
    }

    // ── GET /api/stock/top ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTop_AlpacaHasSnapshots_Returns200WithSortedQuotes()
    {
        // Arrange
        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["NVDA"] = BuildSnapshot(900m, 880m),
                ["AAPL"] = BuildSnapshot(180m, 175m)
            });

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/stock/top?limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadAsAsync<List<StockQuote>>();
        results.Should().NotBeNull();
        results!.Should().HaveCount(2);
        // Sorted descending by price — NVDA first
        results![0].Symbol.Should().Be("NVDA");
        results![1].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task GetTop_AlpacaReturnsEmpty_Returns200WithEmptyList()
    {
        // Arrange
        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>());

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/stock/top?limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadAsAsync<List<StockQuote>>();
        results.Should().BeEmpty();
    }

    // ── GET /api/stock/{symbol} ────────────────────────────────────────────────

    [Fact]
    public async Task GetDetails_KnownSymbol_Returns200WithDetails()
    {
        // Arrange
        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["MSFT"] = BuildSnapshot(410m, 400m)
            });

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/stock/MSFT");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadAsAsync<StockDetailDto>();
        detail.Should().NotBeNull();
        detail!.Symbol.Should().Be("MSFT");
        detail.CurrentPrice.Should().Be(410m);
    }

    [Fact]
    public async Task GetDetails_UnknownSymbol_Returns404()
    {
        // Arrange — Alpaca returns no snapshot for this symbol
        _factory.AlpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>());

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/stock/DOESNOTEXIST");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AlpacaSnapshot BuildSnapshot(decimal price, decimal prevClose) =>
        new()
        {
            LatestTrade = new SnapshotTrade { Price = price },
            DailyBar = new SnapshotBar { Close = price, Timestamp = DateTime.UtcNow },
            PrevDailyBar = new SnapshotBar { Close = prevClose }
        };
}
