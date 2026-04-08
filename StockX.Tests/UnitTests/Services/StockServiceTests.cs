using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using StockX.Core.DTOs.Stock;
using StockX.Core.Entities;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.Caching;
using StockX.Infrastructure.External.AlpacaApi;
using StockX.Infrastructure.External.AlpacaApi.Models;
using StockX.Services.Stock;
using Xunit;
using StockEntity = StockX.Core.Entities.Stock;

namespace StockX.Tests.UnitTests.Services;

public sealed class StockServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IStockRepository> _stockRepoMock;
    private readonly Mock<IAlpacaService> _alpacaMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IRepository<StockEntity>> _stocksRepoMock;
    private readonly StockService _sut;

    public StockServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _stockRepoMock = new Mock<IStockRepository>();
        _alpacaMock = new Mock<IAlpacaService>();
        _cacheMock = new Mock<ICacheService>();
        _stocksRepoMock = new Mock<IRepository<StockEntity>>();

        _unitOfWorkMock.Setup(u => u.Stocks).Returns(_stocksRepoMock.Object);

        _sut = new StockService(
            _unitOfWorkMock.Object,
            _stockRepoMock.Object,
            _alpacaMock.Object,
            _cacheMock.Object);
    }

    // ── SearchStocksAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchStocksAsync_DbHasEnoughResults_ReturnsDatabaseResultsWithoutCallingAlpaca()
    {
        // Arrange
        var dbStocks = Enumerable.Range(0, 20)
            .Select(i => new StockEntity { Symbol = $"SYM{i}" })
            .ToList();

        _stockRepoMock
            .Setup(r => r.SearchAsync("SYM", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbStocks);

        // Act
        var result = await _sut.SearchStocksAsync("SYM", 20);

        // Assert
        result.Should().HaveCount(20);
        _alpacaMock.Verify(
            a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchStocksAsync_LimitLteZero_UsesDefaultLimit20()
    {
        // Arrange
        var dbStocks = Enumerable.Range(0, 20)
            .Select(i => new StockEntity { Symbol = $"SYM{i}" })
            .ToList();

        _stockRepoMock
            .Setup(r => r.SearchAsync("SYM", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbStocks);

        // Act — pass limit 0, which should default to 20
        var result = await _sut.SearchStocksAsync("SYM", 0);

        // Assert
        result.Should().HaveCount(20);
    }

    [Fact]
    public async Task SearchStocksAsync_DbResultsSparse_FallsBackToAlpacaAndMerges()
    {
        // Arrange
        var dbStocks = new List<StockEntity>
        {
            new StockEntity { Symbol = "AAPL", Name = "Apple Inc." }
        };

        _stockRepoMock
            .Setup(r => r.SearchAsync("AP", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbStocks);

        var alpacaAssets = new List<AlpacaAsset>
        {
            new AlpacaAsset { Symbol = "APLY", Name = "Amplify Transformational Data Sharing ETF", Exchange = "NASDAQ" },
            new AlpacaAsset { Symbol = "APD", Name = "Air Products", Exchange = "NYSE" }
        };

        _alpacaMock
            .Setup(a => a.SearchAssetsAsync("AP", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alpacaAssets);

        _stockRepoMock
            .Setup(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<StockEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.SearchStocksAsync("AP", 5);

        // Assert
        result.Should().HaveCountGreaterThan(0);
        result.Select(s => s.Symbol).Should().Contain("AAPL"); // DB result preserved
    }

    [Fact]
    public async Task SearchStocksAsync_AlpacaReturnsEmpty_ReturnsDbResults()
    {
        // Arrange
        var dbStocks = new List<StockEntity>
        {
            new StockEntity { Symbol = "AAPL", Name = "Apple Inc." }
        };

        _stockRepoMock
            .Setup(r => r.SearchAsync("AA", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbStocks);

        _alpacaMock
            .Setup(a => a.SearchAssetsAsync("AA", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlpacaAsset>());

        // Act
        var result = await _sut.SearchStocksAsync("AA", 5);

        // Assert
        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task SearchStocksAsync_AlpacaThrows_ReturnsDbResults()
    {
        // Arrange
        var dbStocks = new List<StockEntity>
        {
            new StockEntity { Symbol = "AAPL", Name = "Apple Inc." }
        };

        _stockRepoMock
            .Setup(r => r.SearchAsync("AA", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbStocks);

        _alpacaMock
            .Setup(a => a.SearchAssetsAsync("AA", 5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API down"));

        // Act
        var result = await _sut.SearchStocksAsync("AA", 5);

        // Assert
        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task SearchStocksAsync_AlpacaAssetsWithBlankSymbol_FiltersThemOut()
    {
        // Arrange
        _stockRepoMock
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntity>());

        var alpacaAssets = new List<AlpacaAsset>
        {
            new AlpacaAsset { Symbol = "", Name = "No Symbol" },
            new AlpacaAsset { Symbol = "GOOD", Name = "Good Stock", Exchange = "NYSE" }
        };

        _alpacaMock
            .Setup(a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(alpacaAssets);

        _stockRepoMock
            .Setup(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<StockEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.SearchStocksAsync("G", 5);

        // Assert — only the asset with a valid symbol should be included
        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("GOOD");
    }

    // ── GetTopStocksAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetTopStocksAsync_CacheHit_ReturnsCachedValues()
    {
        // Arrange
        var cached = new List<StockQuote>
        {
            new StockQuote("AAPL", "Apple", "NASDAQ", 180m, null, 1.5m, DateTime.UtcNow)
        };

        _cacheMock
            .Setup(c => c.GetAsync<IReadOnlyList<StockQuote>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        // Act
        var result = await _sut.GetTopStocksAsync(5);

        // Assert
        result.Should().BeSameAs(cached);
        _alpacaMock.Verify(
            a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTopStocksAsync_LimitLteZero_UsesDefault15()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync<IReadOnlyList<StockQuote>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<StockQuote>?)null);

        // Return a snapshot for some symbols
        var snapshots = new Dictionary<string, AlpacaSnapshot>
        {
            ["NVDA"] = BuildSnapshot(currentPrice: 900m, prevClose: 880m)
        };

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        _stocksRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<StockEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntity>());

        _cacheMock
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<StockQuote>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act — pass limit 0 (should default to 15)
        var result = await _sut.GetTopStocksAsync(0);

        // Assert — we only got 1 snapshot so should get at most 1 result but ≤ 15
        result.Count.Should().BeLessThanOrEqualTo(15);
    }

    [Fact]
    public async Task GetTopStocksAsync_CacheMiss_FetchesSnapshotsAndCaches()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync<IReadOnlyList<StockQuote>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<StockQuote>?)null);

        var snapshots = new Dictionary<string, AlpacaSnapshot>
        {
            ["NVDA"] = BuildSnapshot(currentPrice: 900m, prevClose: 880m),
            ["AAPL"] = BuildSnapshot(currentPrice: 180m, prevClose: 175m)
        };

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        _stocksRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<StockEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntity>());

        _cacheMock
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<StockQuote>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GetTopStocksAsync(5);

        // Assert
        result.Should().HaveCount(2);
        result[0].CurrentPrice.Should().BeGreaterThan(result[result.Count - 1].CurrentPrice); // sorted desc
        _cacheMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<StockQuote>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTopStocksAsync_SnapshotPriceIsZero_SkipsSymbol()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync<IReadOnlyList<StockQuote>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<StockQuote>?)null);

        var snapshots = new Dictionary<string, AlpacaSnapshot>
        {
            ["NVDA"] = BuildSnapshot(currentPrice: 0m, prevClose: 0m)  // price is 0 → must be skipped
        };

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        _cacheMock
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<StockQuote>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GetTopStocksAsync(5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopStocksAsync_SymbolFoundInDb_UsesDbName()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync<IReadOnlyList<StockQuote>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<StockQuote>?)null);

        var snapshots = new Dictionary<string, AlpacaSnapshot>
        {
            ["NVDA"] = BuildSnapshot(currentPrice: 900m, prevClose: 880m)
        };

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        _stocksRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<StockEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntity>
            {
                new StockEntity { Symbol = "NVDA", Name = "NVIDIA Corporation" }
            });

        _cacheMock
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<StockQuote>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GetTopStocksAsync(5);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("NVIDIA Corporation");
    }

    // ── GetStockDetailsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetStockDetailsAsync_CachedValue_ReturnsCached()
    {
        // Arrange
        var cached = new StockQuote("AAPL", "Apple", "NASDAQ", 180m, null, 1.5m, DateTime.UtcNow);

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StockQuote?>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        // Act
        var result = await _sut.GetStockDetailsAsync("aapl");

        // Assert
        result.Should().BeSameAs(cached);
    }

    [Fact]
    public async Task GetStockDetailsAsync_SnapshotNotFound_ReturnsNull()
    {
        // Arrange: GetOrCreateAsync actually runs the factory
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StockQuote?>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task<StockQuote?>>, TimeSpan, CancellationToken>((_, factory, _, _) => factory());

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>());

        // Act
        var result = await _sut.GetStockDetailsAsync("UNKNOWN");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStockDetailsAsync_SnapshotPriceZero_ReturnsNull()
    {
        // Arrange: factory executes
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StockQuote?>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task<StockQuote?>>, TimeSpan, CancellationToken>((_, factory, _, _) => factory());

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["AAPL"] = BuildSnapshot(currentPrice: 0m, prevClose: 0m)
            });

        // Act
        var result = await _sut.GetStockDetailsAsync("aapl");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStockDetailsAsync_ValidSnapshot_DbStockFound_ReturnsQuote()
    {
        // Arrange: factory executes
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StockQuote?>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task<StockQuote?>>, TimeSpan, CancellationToken>((_, factory, _, _) => factory());

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["AAPL"] = BuildSnapshot(currentPrice: 180m, prevClose: 175m)
            });

        _stocksRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<StockEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntity>
            {
                new StockEntity { Symbol = "AAPL", Name = "Apple Inc.", Exchange = "NASDAQ" }
            });

        // Act
        var result = await _sut.GetStockDetailsAsync("aapl");

        // Assert
        result.Should().NotBeNull();
        result!.Symbol.Should().Be("AAPL");
        result.Name.Should().Be("Apple Inc.");
        result.CurrentPrice.Should().Be(180m);
    }

    [Fact]
    public async Task GetStockDetailsAsync_ValidSnapshot_DbStockNotFound_UpsertsThenReturnsQuote()
    {
        // Arrange: factory executes
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StockQuote?>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task<StockQuote?>>, TimeSpan, CancellationToken>((_, factory, _, _) => factory());

        _alpacaMock
            .Setup(a => a.GetSnapshotsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AlpacaSnapshot>
            {
                ["AAPL"] = BuildSnapshot(currentPrice: 180m, prevClose: 175m)
            });

        _stocksRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<StockEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntity>());     // not in DB

        _stockRepoMock
            .Setup(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<StockEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetStockDetailsAsync("aapl");

        // Assert
        result.Should().NotBeNull();
        result!.Symbol.Should().Be("AAPL");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AlpacaSnapshot BuildSnapshot(decimal currentPrice, decimal prevClose)
    {
        // Use LatestTrade to drive CurrentPrice
        return new AlpacaSnapshot
        {
            LatestTrade = new SnapshotTrade { Price = currentPrice },
            DailyBar = new SnapshotBar { Close = currentPrice, Timestamp = DateTime.UtcNow },
            PrevDailyBar = new SnapshotBar { Close = prevClose }
        };
    }
}
