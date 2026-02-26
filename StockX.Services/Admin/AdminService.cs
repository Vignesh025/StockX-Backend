using StockX.Core.DTOs.Admin;
using StockX.Core.DTOs.Common;
using StockX.Core.DTOs.Portfolio;
using StockX.Core.Entities;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Admin;

public sealed class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWalletService _walletService;
    private readonly ITradingService _tradingService;
    private readonly ITransactionRepository _transactionRepository;

    public AdminService(
        IUnitOfWork unitOfWork,
        IWalletService walletService,
        ITradingService tradingService,
        ITransactionRepository transactionRepository)
    {
        _unitOfWork = unitOfWork;
        _walletService = walletService;
        _tradingService = tradingService;
        _transactionRepository = transactionRepository;
    }

    public async Task<PagedResult<AdminUserListItem>> GetUsersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 20;
        }

        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users
                .Where(u => u.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = users.Count;
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var pagedUsers = users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = new List<AdminUserListItem>();

        foreach (var user in pagedUsers)
        {
            var balance = await _walletService.CalculateWalletBalanceAsync(user.UserId, cancellationToken);

            items.Add(
                new AdminUserListItem(
                    user.UserId,
                    user.Name,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    balance));
        }

        return new PagedResult<AdminUserListItem>(
            items,
            total,
            page,
            pageSize,
            totalPages);
    }

    public async Task<AdminUserDetail?> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var walletBalance = await _walletService.CalculateWalletBalanceAsync(userId, cancellationToken);
        var portfolio = await _tradingService.GetPortfolioAsync(userId, cancellationToken);
        var recentTransactions = await _transactionRepository.GetRecentByUserAsync(
            userId,
            10,
            cancellationToken);

        var portfolioSummary = new PortfolioSummary(
            portfolio.Holdings,
            portfolio.TotalValue,
            portfolio.TotalCost,
            portfolio.TotalProfitLoss);

        return new AdminUserDetail(
            user.UserId,
            user.Name,
            user.Email,
            user.Role,
            user.IsActive,
            walletBalance,
            portfolioSummary,
            recentTransactions);
    }
}

