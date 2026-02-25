using StockX.Core.Entities;

namespace StockX.Core.Interfaces.Repositories;

public interface IStockRepository : IRepository<Stock>
{
    Task<IReadOnlyList<Stock>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}

