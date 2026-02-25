using StockX.Core.DTOs.Wallet;
using StockX.Core.Entities;
using StockX.Core.Enums;

namespace StockX.Core.Services.Interfaces;

public interface IWalletService
{
    Task<WalletBalanceSummary> GetWalletBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<decimal> CalculateWalletBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        Guid userId,
        TransactionType? type,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);
}

