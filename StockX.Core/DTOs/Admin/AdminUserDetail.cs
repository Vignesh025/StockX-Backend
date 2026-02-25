using StockX.Core.DTOs.Portfolio;
using StockX.Core.Entities;
using StockX.Core.Enums;

namespace StockX.Core.DTOs.Admin;

public sealed record AdminUserDetail(
    Guid UserId,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    decimal WalletBalance,
    PortfolioSummary PortfolioSummary,
    IReadOnlyList<Transaction> RecentTransactions);

