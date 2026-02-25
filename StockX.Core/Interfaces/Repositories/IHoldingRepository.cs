using StockX.Core.Entities;

namespace StockX.Core.Interfaces.Repositories;

public interface IHoldingRepository : IRepository<UserStockHolding>
{
    Task<UserStockHolding?> GetForUserAndSymbolAsync(
        Guid userId,
        string stockSymbol,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserStockHolding>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

