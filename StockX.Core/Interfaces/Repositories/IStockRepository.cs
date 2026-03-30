using StockX.Core.Entities;

namespace StockX.Core.Interfaces.Repositories;

public interface IStockRepository : IRepository<Stock>
{
    Task<IReadOnlyList<Stock>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts stocks whose Symbol does not already exist in the DB.
    /// Returns the number of new rows inserted.
    /// </summary>
    Task<int> UpsertRangeAsync(
        IEnumerable<Stock> stocks,
        CancellationToken cancellationToken = default);
}

