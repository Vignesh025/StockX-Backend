using StockX.Core.Entities;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.Persistence.Context;
using StockX.Infrastructure.Persistence.Repositories;

namespace StockX.Infrastructure.Persistence.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;

        Users = new Repository<User>(_dbContext);
        Stocks = new Repository<Stock>(_dbContext);
        Holdings = new Repository<UserStockHolding>(_dbContext);
        Transactions = new Repository<Transaction>(_dbContext);
        PaymentIntents = new Repository<PaymentIntent>(_dbContext);
    }

    public IRepository<User> Users { get; }

    public IRepository<Stock> Stocks { get; }

    public IRepository<UserStockHolding> Holdings { get; }

    public IRepository<Transaction> Transactions { get; }

    public IRepository<PaymentIntent> PaymentIntents { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }
}

