using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Infrastructure.Persistence.Context;
using StockX.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StockX.Tests.UnitTests.Repositories;

public sealed class StockRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly StockRepository _sut;

    public StockRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new StockRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── SearchAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        SeedStocks("AAPL", "MSFT");

        var result = await _sut.SearchAsync("", 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhitespaceQuery_ReturnsEmpty()
    {
        SeedStocks("AAPL");

        var result = await _sut.SearchAsync("   ", 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_MatchingSymbol_ReturnsMatchingStocks()
    {
        SeedStocks("AAPL", "MSFT", "AMD");

        var result = await _sut.SearchAsync("AA", 10);

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task SearchAsync_MatchingName_ReturnsMatchingStocks()
    {
        _dbContext.Stocks.AddRange(
            new Stock { Symbol = "AAPL", Name = "Apple Inc.", Exchange = "NASDAQ", AssetType = "US Equity" },
            new Stock { Symbol = "AMZN", Name = "Amazon.com Inc.", Exchange = "NASDAQ", AssetType = "US Equity" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _sut.SearchAsync("amazon", 10);

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("AMZN");
    }

    [Fact]
    public async Task SearchAsync_LimitLteZero_DefaultsTo10()
    {
        // Seed 15 stocks that all match
        for (var i = 0; i < 15; i++)
        {
            _dbContext.Stocks.Add(new Stock
            {
                Symbol = $"SYM{i:D2}",
                Name = "Test Stock",
                Exchange = "NYSE",
                AssetType = "US Equity"
            });
        }
        await _dbContext.SaveChangesAsync();

        var result = await _sut.SearchAsync("SYM", 0);

        result.Should().HaveCount(10); // default limit
    }

    [Fact]
    public async Task SearchAsync_LimitApplied_ReturnsTruncatedResults()
    {
        for (var i = 0; i < 10; i++)
        {
            _dbContext.Stocks.Add(new Stock
            {
                Symbol = $"AA{i:D2}",
                Name = $"Stock {i}",
                Exchange = "NYSE",
                AssetType = "US Equity"
            });
        }
        await _dbContext.SaveChangesAsync();

        var result = await _sut.SearchAsync("AA", 3);

        result.Should().HaveCount(3);
    }

    // ── CountAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CountAsync_NoStocks_ReturnsZero()
    {
        var result = await _sut.CountAsync();
        result.Should().Be(0);
    }

    [Fact]
    public async Task CountAsync_WithStocks_ReturnsCorrectCount()
    {
        SeedStocks("AAPL", "MSFT", "GOOG");

        var result = await _sut.CountAsync();

        result.Should().Be(3);
    }

    // ── UpsertRangeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertRangeAsync_EmptyList_ReturnsZero()
    {
        var result = await _sut.UpsertRangeAsync(Array.Empty<Stock>());
        result.Should().Be(0);
    }

    [Fact]
    public async Task UpsertRangeAsync_NewStocks_InsertsAndReturnsCount()
    {
        var stocks = new[]
        {
            new Stock { Symbol = "NEW1", Name = "New Stock 1", Exchange = "NYSE", AssetType = "US Equity" },
            new Stock { Symbol = "NEW2", Name = "New Stock 2", Exchange = "NASDAQ", AssetType = "US Equity" }
        };

        var inserted = await _sut.UpsertRangeAsync(stocks);

        inserted.Should().Be(2);
        _dbContext.Stocks.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpsertRangeAsync_ExistingStocks_SkipsDuplicates()
    {
        // Pre-seed AAPL
        _dbContext.Stocks.Add(new Stock { Symbol = "AAPL", Name = "Apple Inc.", Exchange = "NASDAQ", AssetType = "US Equity" });
        await _dbContext.SaveChangesAsync();

        // Try to upsert AAPL (existing) + MSFT (new)
        var stocks = new[]
        {
            new Stock { Symbol = "AAPL", Name = "Apple UPDATED", Exchange = "NASDAQ", AssetType = "US Equity" },
            new Stock { Symbol = "MSFT", Name = "Microsoft", Exchange = "NASDAQ", AssetType = "US Equity" }
        };

        var inserted = await _sut.UpsertRangeAsync(stocks);

        inserted.Should().Be(1); // only MSFT inserted
        _dbContext.Stocks.Should().HaveCount(2);
        // Original AAPL name should remain unchanged
        var aapl = await _dbContext.Stocks.FindAsync("AAPL");
        aapl!.Name.Should().Be("Apple Inc.");
    }

    [Fact]
    public async Task UpsertRangeAsync_DuplicateSymbolsInInput_DeduplicatesAndInsertsOnce()
    {
        var stocks = new[]
        {
            new Stock { Symbol = "DUP", Name = "First", Exchange = "NYSE", AssetType = "US Equity" },
            new Stock { Symbol = "DUP", Name = "Second", Exchange = "NYSE", AssetType = "US Equity" }
        };

        var inserted = await _sut.UpsertRangeAsync(stocks);

        inserted.Should().Be(1); // only one inserted
        _dbContext.Stocks.Should().HaveCount(1);
    }

    private void SeedStocks(params string[] symbols)
    {
        foreach (var sym in symbols)
        {
            _dbContext.Stocks.Add(new Stock
            {
                Symbol = sym,
                Name = $"{sym} Corp",
                Exchange = "NYSE",
                AssetType = "US Equity"
            });
        }
        _dbContext.SaveChanges();
    }
}
