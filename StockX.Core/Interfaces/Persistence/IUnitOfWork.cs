using StockX.Core.Entities;
using StockX.Core.Interfaces.Repositories;

namespace StockX.Core.Interfaces.Persistence;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<User> Users { get; }

    IRepository<Stock> Stocks { get; }

    IRepository<UserStockHolding> Holdings { get; }

    IRepository<Transaction> Transactions { get; }

    IRepository<PaymentIntent> PaymentIntents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

