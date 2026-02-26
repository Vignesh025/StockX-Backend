using StockX.Core.DTOs.Wallet;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Wallet;

public sealed class WalletService : IWalletService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionRepository _transactionRepository;

    public WalletService(
        IUnitOfWork unitOfWork,
        ITransactionRepository transactionRepository)
    {
        _unitOfWork = unitOfWork;
        _transactionRepository = transactionRepository;
    }

    public async Task<WalletBalanceSummary> GetWalletBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _unitOfWork.Transactions.FindAsync(
            t => t.UserId == userId && t.Status == TransactionStatus.Completed,
            cancellationToken);

        var balance = transactions.Sum(t => t.Amount);
        var lastUpdated = transactions.Count == 0
            ? DateTime.MinValue
            : transactions.Max(t => t.Timestamp);

        return new WalletBalanceSummary(balance, lastUpdated);
    }

    public async Task<decimal> CalculateWalletBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _unitOfWork.Transactions.FindAsync(
            t => t.UserId == userId && t.Status == TransactionStatus.Completed,
            cancellationToken);

        return transactions.Sum(t => t.Amount);
    }

    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        Guid userId,
        TransactionType? type,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        return _transactionRepository.GetByUserAsync(
            userId,
            type,
            limit,
            offset,
            cancellationToken);
    }
}

