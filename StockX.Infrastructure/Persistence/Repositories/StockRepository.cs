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
}

