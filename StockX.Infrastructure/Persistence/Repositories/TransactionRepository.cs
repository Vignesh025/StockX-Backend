using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.Persistence.Context;

namespace StockX.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Transaction>> GetByUserAsync(
        Guid userId,
        TransactionType? type,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        limit = limit <= 0 ? 50 : limit;
        offset = offset < 0 ? 0 : offset;

        IQueryable<Transaction> query = DbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        if (type is not null)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        return await query
            .OrderByDescending(t => t.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetRecentByUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = limit <= 0 ? 10 : limit;

        return await DbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

