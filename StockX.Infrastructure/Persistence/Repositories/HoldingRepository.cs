using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.Persistence.Context;

namespace StockX.Infrastructure.Persistence.Repositories;

public sealed class HoldingRepository : Repository<UserStockHolding>, IHoldingRepository
{
    public HoldingRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public Task<UserStockHolding?> GetForUserAndSymbolAsync(
        Guid userId,
        string stockSymbol,
        CancellationToken cancellationToken = default)
    {
        return DbContext.UserStockHoldings
            .FirstOrDefaultAsync(
                h => h.UserId == userId && h.StockSymbol == stockSymbol,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserStockHolding>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.UserStockHoldings
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderBy(h => h.StockSymbol)
            .ToListAsync(cancellationToken);
    }
}

