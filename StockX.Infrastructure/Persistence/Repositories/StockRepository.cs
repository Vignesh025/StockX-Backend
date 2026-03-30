using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.Persistence.Context;

namespace StockX.Infrastructure.Persistence.Repositories;

public sealed class StockRepository : Repository<Stock>, IStockRepository
{
    public StockRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Stock>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        query = (query ?? string.Empty).Trim();
        limit = limit <= 0 ? 10 : limit;

        if (query.Length == 0)
        {
            return Array.Empty<Stock>();
        }

        var q = query.ToUpperInvariant();

        return await DbContext.Stocks
            .AsNoTracking()
            .Where(s => s.Symbol.ToUpper().Contains(q) || s.Name.ToUpper().Contains(q))
            .OrderBy(s => s.Symbol)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await DbContext.Stocks.CountAsync(cancellationToken);

    public async Task<int> UpsertRangeAsync(
        IEnumerable<Stock> stocks,
        CancellationToken cancellationToken = default)
    {
        var incoming = stocks.ToList();
        if (incoming.Count == 0) return 0;

        // Fetch existing symbols in one round-trip
        var symbols = incoming.Select(s => s.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await DbContext.Stocks
            .Where(s => symbols.Contains(s.Symbol))
            .Select(s => s.Symbol)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var newStocks = incoming
            .Where(s => !existing.Contains(s.Symbol))
            .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (newStocks.Count == 0) return 0;

        await DbContext.Stocks.AddRangeAsync(newStocks, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        return newStocks.Count;
    }
}


