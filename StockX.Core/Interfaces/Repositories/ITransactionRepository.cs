using StockX.Core.Entities;
using StockX.Core.Enums;

namespace StockX.Core.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetByUserAsync(
        Guid userId,
        TransactionType? type,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetRecentByUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}

